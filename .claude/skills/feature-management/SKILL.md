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
> The vocabulary a slice declares features in (`FeatureNames`, `IFeatureDefinitionProvider`,
> `FeatureDefinitionContext`, the value types and validators, `FeatureTarget`) and the service
> interfaces it asks questions of (`IFeatureChecker`, `IFeatureValueResolver`,
> `IFeatureDefinitionService`, `IPermissionFeatureFilter`) carry `[AllowOutside]`, the same way
> `ITenantContext` does. Their implementations are `[NoDirectUse]`, and the value providers
> (`Providers/`) stay private to tenancy. A new public type on that surface needs `[AllowOutside]`, or
> `FeatureDependencyTests` will refuse the first slice that uses it.

## 1. Backend constant

`src/backend/Source/Features/Tenancy/Core/FeatureManagement/FeatureNames.cs` — constant name
`Group_Capability`, value `"Group.Capability"`, exactly like `Allow.cs`:

```csharp
public const string Reporting_Enabled = "Reporting.Enabled";
public const string Reporting_MaxRows = "Reporting.MaxRows";
```

## 2. Declare it in the owning slice's provider

`src/backend/Source/Features/<Feature>/Core/<Feature>FeaturesProvider.cs`. `TenancyFeature` registers
every `IFeatureDefinitionProvider` in the assembly by reflection. Unlike a permission, every node is a
real feature with a value of its own — a parent is not just a display grouping — and a child toggle is
not in force whenever an ancestor toggle is off.

```csharp
namespace Backend.Features.Reporting.Core;

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
template that withheld something by default would break a generated project that has sold nothing.
`Tests/FeatureManagement/ShippedFeatureTests.cs` enforces it: every toggle defaults to enabled, every
default is valid for its own value type, every name has the `Group.Name` shape and a `FeatureNames`
constant.

Value types: `ToggleValueType` (the default), `FreeTextValueType(validator)` and
`SelectionValueType(items)`. Validators are `BooleanValidator`, `NumericValidator`,
`StringLengthValidator`, `SelectionValidator` and `AlwaysValidValidator`; their parameters travel to
the management UI so the editor constrains the input the same way the API will.

`AllowProviders(...)` (names from `FeatureValueProviderNames`: `Tenant`, `Edition`, `Configuration`)
confines a feature to particular value providers — use it for something only a plan should set, never
a single tenant. `isVisibleToClients: false` keeps a feature out of `GET /features/mine`.

A value resolves through the chain tenant override → edition → the `FeatureManagement` configuration
section → declared default, first answer winning.

## 3. Mirror the constant on the web app

`src/frontend/web/feature-names.ts`, and add the key to `feature-names.test.ts`, which pins the two
sides together.

## 4. Use it

**Gate a permission on it** — the usual case, and the one that needs no other code:

```csharp
var usersPermissions = context.AddPermission("Users", "Users", PermissionScope.Both)
                              .RequireFeatures(FeatureNames.Identity_UserManagement);
usersPermissions.AddChild(Allow.User_View, "View");
```

Declared on a group node it reaches every permission beneath it. `IPermissionFeatureFilter` removes
the permission from the session (`SessionGrants`) and from the role permission surface
(`GetDefinePermissionsEndpoint`); what the web app is told (`GetInfoEndpoint`) is the session's own
permissions, so it agrees by construction, and the existing
`Permissions(Allow.X)` and `isAllowed` checks do the rest. Details and the two architecture rules
(`Tests/Architect/PermissionFeatureDeclarationTests.cs`: no `Platform` permission may require a
feature; every required feature must be declared) are in the `permissions` skill. The permissions
that administer entitlements (`Edition_*`, `FeatureValue_*`) are `Platform`-scoped and so can never be
gated — keep it that way, or a feature switched off could not be switched back on.

**Check it directly** — for a limit, or a capability with no permission to hang it on. Inject
`IFeatureChecker`:

```csharp
await featureChecker.CheckEnabledAsync(FeatureNames.Reporting_Enabled, ct);   // 403 featureDisabled
var maxRows = await featureChecker.GetAsync(FeatureNames.Reporting_MaxRows, 1000L, ct);
if (rows > maxRows)
{
    throw new FeatureLimitExceededException(FeatureNames.Reporting_MaxRows, maxRows);   // 403 featureLimitExceeded
}
```

`IsEnabledAsync` walks the toggles above the feature and answers `false` for a name nobody declared.
`GetAsync<T>` / `GetOrNullAsync` return the raw value without looking at parent toggles, so check the
parent first when a limit sits under one. `ExceptionProcessor` answers `FeatureDisabledException` and
`FeatureLimitExceededException` with 403 and the codes above.

`IFeatureChecker` reads the target from `ITenantContext`: inside a tenant it answers for that tenant,
in platform scope for the platform (configuration and defaults — no plan), and with no scope
established it throws `TenantScopeNotEstablishedException`. Platform scope is inside no plan, so an
endpoint reachable there should enforce the plan only when a tenant is active, as
`FileUploadEndpoint` does:

```csharp
if (tenantContext.IsResolved && tenantContext.CurrentTenantId is not null)
{
    await featureChecker.CheckEnabledAsync(FeatureNames.FileManagement_Enabled, ct);
}
```

Work that runs **outside** a request — a queued or scheduled job — has no scope; resolve for the
tenant it acts for explicitly with
`IFeatureValueResolver.ResolveAsync(FeatureTarget.ForTenant(tenantId), ct)` and read the returned
`FeatureValueSet` (`IsEnabled`, `GetOrNull`). See the `multi-tenancy` skill for scopes.

**Limits on a count** — accounts, projects, anything a tenant accumulates — are a read followed by a
write, so two concurrent requests can both see room for one more. Check the limit while holding a lock
on the tenant, inside the transaction that writes the new row. `Identity.MaxUserCount` shows how:
`TenantMembershipService.AddAsync` checks it under the tenant lock, and a write path outside that
service calls `ITenantMembershipService.ReserveSeatAsync` inside its own transaction
(`UserCreateEndpoint`). If the web app should show the limit before the user hits it, have one method
compute both the count and the limit, as `ITenantMembershipService.GetSeatsAsync` does (`TenantSeats`
with `Used`, `Limit`, `IsFull`), and use it in the guard and in the endpoint the web app reads
(`GET /users/seats`, `GET /tenants/{tenantId}/members/seats`), so the UI and the API cannot disagree.

**On the web app**, `useFeature(FeatureNames.X)` (`hooks/use-feature.ts`) reports
`{ isEnabled, value, isLoading }` for the caller's own plan from `GET /features/mine`
(`useMyFeaturesQuery`). Most screens do not need it: a permission gated on a feature is already
absent, so `isAllowed` has hidden the action. Use it for a limit, or where you want to offer an
upgrade rather than show nothing. `usePlanMaxUploadBytes()` is the worked example for a limit: it asks
only while a tenant is active, and `FileUpload` / `MultiFileUpload` apply the stricter of it and their
own `maxSizeBytes`.

## 5. When a plan change takes effect

Plan gating is **computed when the session is created, and the session is revoked when the plan
changes**. `SessionGrants` applies `IPermissionFeatureFilter` once, when sign-in, refresh or a tenant
switch writes the session to the store; every request then reads that stored session. So each
endpoint that changes a plan ends the sessions it affects through the `[AllowOutside]`
`Backend.Features.Identity.Core.Sessions.ISessionRevocationService`, after its change commits:

| Change | Sessions ended |
|---|---|
| `TenantUpdateEndpoint` changing the tenant's edition (a rename ends none) | every session in that tenant |
| `FeatureValueUpdateEndpoint` naming a tenant | every session in that tenant |
| `FeatureValueUpdateEndpoint` naming an edition, or `EditionDeleteEndpoint` | every session in every tenant on that edition |

The old access token answers 401 on its next request and its refresh token is refused, so the
tenant's users come back under the new plan when they sign in again. A new endpoint that changes what
a tenant's plan grants must revoke the same way — never inside the transaction, never by touching the
session store. Platform-scope sessions are inside no plan, so a plan change never targets them (one whose refresh row still records the tenant may be ended with it, which errs on the safe side). A
change to the `FeatureManagement` configuration section revokes nothing and reaches a session only when
it is next replaced. (`IFeatureChecker` calls are unaffected by any of this: they read the current value
on every call.)

Gating never removes a grant. Role grants stay in the database while the feature is off, so turning
it back on restores the permission with nothing to re-grant, and `ChangePermissionsEndpoint` carries
plan-hidden grants through a replacement rather than reading their absence from the form as a removal.

## 6. Tests

Add cases to `src/backend/Tests/FeatureManagement/`, deriving from `FeatureTestsBase`
(`CreateEditionAsync`, `CreateTenantOnEditionAsync`, `SetForTenantAsync`, `SetForEditionAsync`,
`ResolveForTenantAsync`). A test that writes a feature value writes it for a tenant or edition it
created itself; one that touches a seeded tenant, or the whole table, declares
`[Collection("FeatureManagement")]`, because a feature value changes what other tests' sessions are
created with — and ends the sessions of the tenant it names. After changing a value through its
endpoint, sign in again before asserting on permissions: the old token answers 401. A value written
straight through the service or `DbContext` revokes nothing, so a session created before it keeps the
old grants until it is replaced.

## Checklist

- [ ] Constant in `FeatureNames.cs`
- [ ] Definition in the slice's `<X>FeaturesProvider`, defaulting to enabled
- [ ] Mirrored in `feature-names.ts` and `feature-names.test.ts`
- [ ] Gated a permission with `RequireFeatures`, or called the checker (a limit throws `FeatureLimitExceededException`)
- [ ] Plan checks skipped in platform scope, and resolved for a named tenant outside a request
- [ ] Test covering both the enabled and the disabled case
