---
name: backend-feature
description: Create a new vertical-slice feature under src/backend/Source/Features (Core + Endpoints + Feature.cs + permission and entitlement providers), or work across existing features. Use when adding a whole capability rather than a single endpoint, and whenever cross-feature dependency, [AllowOutside] or [NoDirectUse] questions come up.
---

# Creating a backend feature

A feature is a vertical slice. Everything it owns — entities, EF configuration, services,
settings, permissions, entitlements, endpoints — lives under `src/backend/Source/Features/<Feature>/`.
`Identity`, `Tenancy`, `Notifications` and `FileManagement` are the worked examples.

## Layout

```
Features/<Feature>/
  <Feature>Feature.cs                    # IFeature.AddServices — the slice's DI module
  Core/
    <Feature>PermissionsProvider.cs      # IPermissionDefinitionProvider (only if it owns permissions)
    <Feature>FeaturesProvider.cs         # IFeatureDefinitionProvider (only if it owns entitlements)
    <Feature>SettingsProvider.cs         # ISettingDefinitionProvider (only if it owns run-time settings)
    <Name>Service.cs                     # interface + implementation in one file
    <Name>Query.cs                       # optional narrow read contract for other features
    <Entity>ValidationRules.cs           # optional FluentValidation rules shared by several endpoints
    <Feature>Dtos.cs                     # optional DTOs the published services answer in
    <Feature>Constants.cs, Enums.cs
    <Name>Setting.cs                     # options bound from configuration
    Entities/
      <Entity>.cs
      Configuration/<Entity>Configuration.cs
  Endpoints/
    <Area>/
      <Area>Group.cs
      <Entity><Action>Endpoint.cs
```

## 1. The feature module

```csharp
namespace Backend.Features.Notifications;

using Backend.Features.Notifications.Core;

/// <summary>
/// Feature module that registers the notifications services with the DI container.
/// </summary>
[BypassNoDirectUse]
public class NotificationsFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddScoped<INotificationService, NotificationService>();
    }
}
```

`Helper.AddFeatures` reflects over the assembly at startup and invokes every `IFeature.AddServices`
— **never** register a feature's services by hand in `Program.cs`. `[BypassNoDirectUse]` is needed
because this class references concrete implementations on purpose. Register services **scoped**:
they depend on the scoped `AppDbContext` and `ITenantContext`, and a request and a background job
must each get their own tenant scope.

Options are bound here too, with validation and `ValidateOnStart()`:

```csharp
services.AddOptions<AuthSetting>()
    .Bind(configuration.GetRequiredSection("Auth"))
    .Validate(setting => setting.AccessTokenValidity > 0 && setting.RefreshTokenValidity > 0,
        "Authentication token lifetimes must be positive.")
    .ValidateOnStart();
```

Add the section to `src/backend/Source/appsettings.json` with a safe default. The per-environment
`appsettings.*.json` files are git-ignored and the hooks refuse to touch them — ask the user to
copy any real value across; a default the Testing host needs belongs in `Program.cs`.

Options are fixed per deployment. A value the platform or a tenant should change at run time is a
**setting** instead — see step 4 and the `settings` skill.

## 2. Services

One file per service, interface first, implementation below, both documented:

```csharp
/// <summary>Defines lookup and lifecycle operations for <see cref="Edition"/>…</summary>
[AllowOutside]                                             // only when another feature consumes it
public interface IEditionService
{
    IQueryable<Edition> Editions();                        // composable; the slice's endpoints narrow it
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);
}

/// <summary>EF Core-backed implementation of <see cref="IEditionService"/>…</summary>
[NoDirectUse]
public class EditionService(AppDbContext dbContext, IFeatureValueStore featureValueStore) : IEditionService { … }
```

- `[NoDirectUse]` makes `Tests/Architect/NoDirectUseTests` fail if any other type depends on the
  concrete class instead of the interface. `[BypassNoDirectUse]` is for the rare type that
  legitimately needs the concrete type (feature modules, composition roots).
- Services answer facts (`NameExistsAsync`); the endpoint turns a refusal into an
  `ErrorCodes` constant (`this.ThrowError(x => x.Name, ErrorCodes.EditionNameAlreadyExists)`), so
  every caller reports the same code and the message comes from the localized resources — never an
  English message constant.
- Async members take `CancellationToken cancellationToken = default` as the last parameter.
- A tenant-aware service injects `ITenantContext` (published by `Tenancy`); it never takes a tenant
  id from a caller's payload. See the `multi-tenancy` skill.

**Shared validation rules.** When several endpoints (or features) validate the same field, declare
the rule once as rule-builder extensions — `Core/TenantValidationRules.cs`,
`Core/EditionValidationRules.cs`:

```csharp
[AllowOutside]
static class EditionValidationRules
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 128;

    public static IRuleBuilderOptions<T, string> EditionName<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must(name => string.IsNullOrWhiteSpace(name)
                          || (name.Trim().Length >= NameMinLength && name.Trim().Length <= NameMaxLength))
            .WithMessage($"The name must be between {NameMinLength} and {NameMaxLength} characters.");
}
```

Each rule passes a blank value through and is paired with `NotEmpty()` at the call site
(`RuleFor(x => x.Name).NotEmpty().EditionName();`), so an empty value raises one failure, not two.

## 3. Entities and DbSets

Entities live in `Core/Entities`, their EF configuration in `Core/Entities/Configuration`.
Configurations are applied automatically, but the `DbSet` is added by hand to
`src/backend/Source/ShareData/AppDbContext.cs` under a comment naming the feature. Every entity must
be tenant-scoped (`IMayHaveTenant` / `IHaveTenant`) or explicitly exempted in
`Tests/Architect/TenantScopingTests`. See the `backend-entity` skill.

## 4. Permissions, entitlements and settings

If the feature guards anything, add `Core/<Feature>PermissionsProvider.cs` implementing
`IPermissionDefinitionProvider`, and give each permission the right `PermissionScope` (`Tenant`,
`Platform` or `Both`). Providers are discovered by reflection in `Program.cs`. See the `permissions`
skill for the full checklist.

If the capability should depend on the tenant's plan, add `Core/<Feature>FeaturesProvider.cs`
implementing `IFeatureDefinitionProvider` (discovered by `TenancyFeature`) — see the
`feature-management` skill.

If an administrator should be able to change how it behaves — for the platform or per tenant — add
`Core/<Feature>SettingsProvider.cs` implementing `ISettingDefinitionProvider` (discovered by
`SettingsFeature`) and read the value through `ISettingProvider` — see the `settings` skill. All three
providers are separate from `<Feature>Feature.cs`, the DI module.

## 5. Endpoints

One folder per area under `Endpoints/`, with a `Group` that owns the route prefix. See the
`backend-endpoint` skill.

## Feature isolation — the rule that breaks builds

`Tests/Architect/FeatureDependencyTests.Features_Should_Not_Have_Unwanted_Dependencies` scans the
compiled assembly (inheritance, fields, properties, parameters, return types **and IL bodies**) and
fails if a type under `Backend.Features.X` touches a type under `Backend.Features.Y`.

The only escape hatch is `[AllowOutside]` on the *depended-upon* type (class, interface, struct or
enum):

```csharp
/// <summary>…</summary>
[AllowOutside]
public interface ITenantContext { … }
```

So when a new feature needs something from another:

1. Prefer duplicating the small thing locally, or moving it to a shared root namespace that is not
   a feature (`Base`, `ShareData`, `Extensions`, `Permissions`, `Exceptions`, `Attributes`).
2. If it genuinely belongs to the other feature and is meant to be shared, mark that type
   `[AllowOutside]` and keep the surface minimal (interface + DTO, not the implementation).

Entities are never published this way. A feature's entities stay behind its services: publish an
interface that answers in ids, DTOs or enums (`TenancyDtos.cs` holds `[AllowOutside]` DTOs such as
`TenantMemberDto`), and mark those answer types `[AllowOutside]` too. When another feature needs a
composable query rather than a materialized answer, return `IQueryable<Guid>` so the caller can fold
it into its own query without naming the row type — `ITenantMembershipQuery.MemberUserIds(tenantId)`.

Watch the container while you do it. If the service you would publish depends on the feature that
wants to call it, taking it there closes a DI cycle and the container refuses to build. Publish a
second, narrower contract instead — one whose implementation takes `AppDbContext` and nothing else
(`TenantMembershipQuery` beside `TenantMembershipService`) — rather than merging the read into the
service that administers the thing.

`Tests/Architect/Features/FeatureA` and `FeatureB` are fixtures that prove the rule works — they
are not real features, so do not model new code on them or "fix" their intentional violations.

## Checklist

- [ ] `Features/<Feature>/<Feature>Feature.cs` with `AddServices`, services registered scoped
- [ ] Settings bound + validated, section with safe defaults added to `appsettings.json`
- [ ] Entities (tenant-scoped or exempted) + configurations, `DbSet` added, migration created
- [ ] Permission provider + constants in `Permissions/Allow.cs` + mirror in `src/frontend/web/allow.ts`
- [ ] Features provider + `FeatureNames.cs` + `src/frontend/web/feature-names.ts`, if plan-gated
- [ ] Settings provider + setting class + validator, if administrators change its behaviour at run time
- [ ] Endpoints with a group, permissions and validators
- [ ] Tests under `src/backend/Tests/Features/<Feature>/…`
- [ ] `dotnet build EasyForNet.slnx` and `dotnet test src/backend/Tests/Backend.Tests.csproj` pass
      (the architecture tests — feature dependency, no-direct-use, tenant scoping — are part of that run)
