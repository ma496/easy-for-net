---
name: feature-management
description: Add or change a feature (entitlement) end-to-end — FeatureNames.cs, the slice's FeaturesProvider, feature-names.ts, and gating a permission with RequireFeatures or checking one with IFeatureChecker. Use when a capability should depend on the tenant's plan rather than on the caller's role.
---

# Adding a feature

A **feature** answers *does this tenant's plan include this at all* — the same answer for everyone in
the tenant, its administrator included. A **permission** answers *may this caller do it*. Reach for a
feature when the thing you are gating is something the platform sells or withholds; reach for a
permission when it is something an administrator grants.

> Naming: "feature" is overloaded in this codebase. `IFeature` / `<X>Feature.cs` is the **vertical
> slice**, registered by `Helper.AddFeatures`. The entitlement system lives inside the tenancy slice,
> in `Backend.Features.Tenancy.Core.FeatureManagement` — an entitlement is a fact about a tenant. Its
> per-slice declarations are `<X>FeaturesProvider` — plural, `Provider` suffix — beside the
> `<X>PermissionsProvider`.
>
> The vocabulary a slice declares features in, and the services it asks questions of, carry
> `[AllowOutside]` so another slice may depend on them — the same way `ITenantContext` is published.
> Everything that resolves or stores a value is `[NoDirectUse]` and stays private to tenancy. Adding a
> new public type to that surface means adding the attribute, or `FeatureDependencyTests` will refuse
> the first slice that uses it.

## 1. Backend constant

`src/backend/Source/Features/Tenancy/Core/FeatureManagement/FeatureNames.cs` — constant name `Group_Capability`, value
`"Group.Capability"`, exactly like `Allow.cs`:

```csharp
public const string Reporting_Export = "Reporting.Export";
```

## 2. Declare it in the owning slice's provider

`src/backend/Source/Features/<Feature>/Core/<Feature>FeaturesProvider.cs`. Unlike a permission, every
node is a real feature with a value of its own — a parent is not just a display grouping — and a child
is not in force whenever an ancestor toggle is off.

```csharp
public class ReportingFeaturesProvider : IFeatureDefinitionProvider
{
    public string GroupName => "Reporting";

    public void Define(FeatureDefinitionContext context)
    {
        var reporting = context.AddFeature(
            FeatureNames.Reporting_Enabled,
            "Reporting",
            BooleanValidator.TrueValue,
            description: "Whether the tenant may run reports at all.");

        reporting.AddChild(
            FeatureNames.Reporting_MaxRows,
            "Maximum rows",
            "10000",
            new FreeTextValueType(new NumericValidator(1, 1_000_000)),
            "The largest report the tenant may run.");
    }
}
```

**Ship every feature enabled** (`BooleanValidator.TrueValue`, or a permissive numeric default). A
template that withheld something by default would break a generated project that has sold nothing, and
an architecture test enforces it.

Value types: `ToggleValueType` (the default), `FreeTextValueType(validator)` and
`SelectionValueType(items)`. Validators are `NumericValidator`, `StringLengthValidator`,
`SelectionValidator` and `AlwaysValidValidator`; their parameters travel to the management UI so the
editor constrains the input the same way the API will.

`AllowProviders(...)` confines a feature to particular value providers — use it for something only a
plan should set, never a single tenant.

## 3. Mirror the constant on the web app

`src/frontend/web/feature-names.ts`, and add the key to `feature-names.test.ts`, which pins the two
sides together.

## 4. Use it

**Gate a permission on it** — the usual case, and the one that needs no other code:

```csharp
var filesPermission = context.AddPermission("Files", "Files", PermissionScope.Both)
                             .RequireFeatures(FeatureNames.FileManagement_Enabled);
filesPermission.AddChild(Allow.File_Delete, "Delete");
```

Declared on a group node it reaches every permission beneath it. The permission is then absent from
the session, from the role permission surface and from what the web app is told, so the existing
`Permissions(Allow.X)` declaration and the existing `isAllowed` check do the rest.

Two rules the architecture tests enforce: a `PermissionScope.Platform` permission may **not** require a
feature (platform scope is inside no plan, so the requirement could never apply), and the permissions
that administer the entitlement system itself must never be gated, or a feature switched off could not
be switched back on.

**Check it directly** — for a limit, or a capability with no permission to hang it on:

```csharp
await featureChecker.CheckEnabledAsync(FeatureNames.Reporting_Export, ct);   // 403 featureDisabled
var maxRows = await featureChecker.GetAsync(FeatureNames.Reporting_MaxRows, 1000, ct);
```

`IFeatureChecker` reads the tenant from `ITenantContext`, so it works in an endpoint or a service
handling a request. Work that runs **outside** a request — a queued or scheduled job — has no scope and
must use `IFeatureValueResolver.ResolveAsync(FeatureTarget.ForTenant(id), ct)`, naming the tenant it
acts for. The checker throws rather than quietly answering for the platform.

On the web app, `useFeature(FeatureNames.X)` reports the caller's own plan. Most screens do not need
it: a permission gated on a feature is already absent, so `isAllowed` has hidden the action. Use it
for a limit, or where you want to offer an upgrade rather than show nothing.

## 5. Mint-time semantics

A feature change takes effect for a caller at their **next session renewal** — sign-in, refresh or
tenant switch — exactly as a role change does. Nothing ends a session that is already running, and the
window is bounded by `Auth:AccessTokenValidity`. Say so in any UI that edits entitlements; the feature
editor already does.

Gating never revokes anything. `DataSeeder` still persists a row for every permission and still grants
the administrator roles the whole scope, so turning a feature back on restores the permission with
nothing to re-grant.

## 6. Tests

Add cases to `src/backend/Tests/FeatureManagement/`. A test that writes a feature value writes it for
a tenant or edition it created itself; one that touches a seeded tenant, or the whole table, declares
`[Collection("FeatureManagement")]`, because a feature value changes what other tests' sessions are
minted with.

## Checklist

- [ ] Constant in `FeatureNames.cs`
- [ ] Definition in the slice's `<X>FeaturesProvider`, defaulting to enabled
- [ ] Mirrored in `feature-names.ts` and `feature-names.test.ts`
- [ ] Gated a permission with `RequireFeatures`, or called the checker
- [ ] Test covering both the enabled and the disabled case
