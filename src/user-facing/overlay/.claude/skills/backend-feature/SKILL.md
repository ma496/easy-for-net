---
name: backend-feature
description: Create a new vertical-slice feature under src/backend/Source/Features (Core + Endpoints + Feature.cs + permission and settings providers), or work across existing features. Use when adding a whole capability rather than a single endpoint, and whenever cross-feature dependency, [AllowOutside] or [NoDirectUse] questions come up.
---

# Creating a backend feature

A feature is a vertical slice. Everything it owns — entities, EF configuration, services,
settings, permissions, endpoints — lives under `src/backend/Source/Features/<Feature>/`.
`Identity`, `Seo`, `Notifications` and `FileManagement` are the worked examples; `Seo` is the
smallest complete one (an entity, a cache, a setting, a permissions provider, admin endpoints and
anonymous public endpoints).

## Layout

```
Features/<Feature>/
  <Feature>Feature.cs                    # IFeature.AddServices — the slice's DI module
  Core/
    <Feature>PermissionsProvider.cs      # IPermissionDefinitionProvider (only if it owns permissions)
    <Feature>SettingsProvider.cs         # ISettingDefinitionProvider (only if it owns run-time settings)
    <Feature>Settings.cs                 # the setting class and its validator
    <Name>Service.cs                     # interface + implementation in one file
    <Feature>ValidationRules.cs          # optional shape checks several endpoints share
    <Feature>Dtos.cs                     # optional DTOs the published services answer in
    <Feature>Constants.cs
    Enums.cs                             # every enum the slice declares, and nothing else
    <Name>Setting.cs                     # options bound from configuration
    Entities/
      <Entity>.cs
      Configuration/<Entity>Configuration.cs
  Endpoints/
    <Area>/
      <Area>Group.cs
      <Area>Contracts.cs                 # optional input/validator/mapper the area's endpoints share
      <Entity><Action>Endpoint.cs
```

## 1. The feature module

```csharp
using Backend.Features.Seo.Core;
using Backend.Features.Settings.Core;

namespace Backend.Features.Seo;

/// <summary>
/// Feature module that registers the SEO services with the DI container.
/// </summary>
[BypassNoDirectUse]
public class SeoFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<SeoCache>();
        services.AddSingleton<ISeoCache>(provider => provider.GetRequiredService<SeoCache>());
        services.AddSingleton<ISettingChangedHandler>(provider => provider.GetRequiredService<SeoCache>());
        services.AddScoped<ISeoSiteService, SeoSiteService>();
    }
}
```

`Helper.AddFeatures` reflects over the assembly at startup and invokes every `IFeature.AddServices`
— **never** register a feature's services by hand in `Program.cs`. `[BypassNoDirectUse]` is needed
because this class references concrete implementations on purpose. Register services **scoped**
when they depend on the scoped `AppDbContext` or `ICurrentUserService`; a singleton is only for
state shared across requests with no scoped dependency (an in-process cache).

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

Options are fixed per deployment. A value an administrator should change at run time is a
**setting** instead — see step 4 and the `settings` skill.

## 2. Services

One file per service, interface first, implementation below, both documented:

```csharp
/// <summary>What every public SEO endpoint needs before it answers…</summary>
public interface ISeoSiteService
{
    Task<SeoSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    string SiteUrl(SeoSettings settings);
}

/// <summary>Default <see cref="ISeoSiteService"/>.</summary>
[NoDirectUse]
public sealed class SeoSiteService(ISettingProvider settingProvider, IOptions<WebSetting> webSetting) : ISeoSiteService { … }
```

- Mark the interface `[AllowOutside]` only when another feature consumes it (`IFileService`,
  `INotificationService`, `ISettingProvider`, `ISessionRevocationService`).
- `[NoDirectUse]` makes `Tests/Architect/NoDirectUseTests` fail if any other type depends on the
  concrete class instead of the interface. `[BypassNoDirectUse]` is for the rare type that
  legitimately needs the concrete type (feature modules, composition roots).
- Services answer facts (`AnyAsync` on a name, a lookup that returns `null`); the endpoint turns a
  refusal into an `ErrorCodes` constant (`this.ThrowError(x => x.Name, ErrorCodes.RoleNameAlreadyExists)`),
  so every caller reports the same code and the message comes from the localized resources — never
  an English message constant.
- Async members take `CancellationToken cancellationToken = default` as the last parameter.
- A service that acts for the caller reads who they are from `ICurrentUserService` (published by
  `Identity`); it never takes a user id from a caller's payload for "my own" data.

**Shared validation rules.** When several endpoints validate the same field shape, declare the
check once in `Core/<Feature>ValidationRules.cs` and call it from each validator
(`SeoValidationRules.IsValidPath`, `IsAbsoluteHttpUrl`, `IsJsonObjectOrArray`), with the column
bounds in `<Feature>Constants.cs`. When a create and an update endpoint take the same fields, one
`AbstractValidator` over a shared input class is pulled in with `Include(...)`
(`SeoPageInputValidator` in `Endpoints/Pages/SeoPageContracts.cs`).

## 3. Entities and DbSets

Entities live in `Core/Entities`, their EF configuration in `Core/Entities/Configuration`.
Configurations are applied automatically, but the `DbSet` is added by hand to
`src/backend/Source/ShareData/AppDbContext.cs` under a comment naming the feature. See the
`backend-entity` skill.

## 4. Permissions and settings

If the feature guards anything, add `Core/<Feature>PermissionsProvider.cs` implementing
`IPermissionDefinitionProvider`. Providers are discovered by reflection in `Program.cs`, and
`DataSeeder` grants every declared permission to the `Admin` role on the next start. A feature with
an admin screen needs its own permissions; the web app adds `Admin.Access` in front of them for
the `/admin` area. See the `permissions` skill for the full checklist.

If an administrator should be able to change how it behaves at run time, add
`Core/<Feature>SettingsProvider.cs` implementing `ISettingDefinitionProvider` (discovered by
`SettingsFeature`) and read the value through `ISettingProvider` — see the `settings` skill. A
feature that caches anything derived from its setting registers an `ISettingChangedHandler`
singleton to drop the cache when the setting is saved (`SeoCache`). Both providers are separate
from `<Feature>Feature.cs`, the DI module.

## 5. Endpoints

One folder per area under `Endpoints/`, with a `Group` that owns the route prefix. A feature
that serves the public site as well as the admin area keeps the two apart in separate areas —
`Seo/Endpoints/Pages` (`seo/pages`, under `Seo.View`/`Seo.Update`) and `Seo/Endpoints/Seo`
(`seo`, anonymous). See the `backend-endpoint` skill.

## Feature isolation — the rule that breaks builds

`Tests/Architect/FeatureDependencyTests.Features_Should_Not_Have_Unwanted_Dependencies` scans the
compiled assembly (inheritance, fields, properties, parameters, return types **and IL bodies**) and
fails if a type under `Backend.Features.X` touches a type under `Backend.Features.Y`.

The only escape hatch is `[AllowOutside]` on the *depended-upon* type (class, interface, struct or
enum):

```csharp
/// <summary>…</summary>
[AllowOutside]
public interface ICultureCatalog { … }
```

So when a new feature needs something from another:

1. Prefer duplicating the small thing locally, or moving it to a shared root namespace that is not
   a feature (`Base`, `ShareData`, `Extensions`, `Permissions`, `Exceptions`, `Attributes`, `Settings`).
2. If it genuinely belongs to the other feature and is meant to be shared, mark that type
   `[AllowOutside]` and keep the surface minimal (interface + DTO, not the implementation).

Entities are never published this way. A feature's entities stay behind its services: publish an
interface that answers in ids, DTOs or enums (`IFileService.OpenPublicAsync` answers a
`FileDownloadResult` with a `FileAccessStatus`), and mark those answer types `[AllowOutside]` too.
When a feature only needs to be *told* about another's change, invert it: the owning feature
publishes a handler interface (`ISettingChangedHandler`, `ISessionEndedHandler`) and the interested
feature implements it, so the owner never names the consumer.

Watch the container while you do it. If the service you would publish depends on the feature that
wants to call it, taking it there closes a DI cycle and the container refuses to build. Publish a
second, narrower contract instead — one whose implementation takes `AppDbContext` and nothing
else — rather than merging the read into the service that administers the thing.

`Tests/Architect/Features/FeatureA` and `FeatureB` are fixtures that prove the rule works — they
are not real features, so do not model new code on them or "fix" their intentional violations.

## Checklist

- [ ] `Features/<Feature>/<Feature>Feature.cs` with `AddServices`, services registered scoped
- [ ] Options bound + validated, section with safe defaults added to `appsettings.json`
- [ ] Entities + configurations, `DbSet` added, migration created
- [ ] Permission provider + constants in `Permissions/Allow.cs` + mirror in `src/frontend/web/allow.ts`
- [ ] Settings provider + setting class + validator, if administrators change its behaviour at run time
- [ ] Endpoints with a group, permissions and validators; anonymous ones return only public data
- [ ] Tests under `src/backend/Tests/Features/<Feature>/…`
- [ ] `dotnet build EasyForNet.slnx` and `dotnet test --project src/backend/Tests/Backend.Tests.csproj` pass
      (the architecture tests — feature dependency, no-direct-use — are part of that run)
