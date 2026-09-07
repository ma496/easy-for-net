---
name: backend-feature
description: Create a new vertical-slice feature under src/backend/Source/Features (Core + Endpoints + Feature.cs + permission provider), or work across existing features. Use when adding a whole capability rather than a single endpoint, and whenever cross-feature dependency, [AllowOutside] or [NoDirectUse] questions come up.
---

# Creating a backend feature

A feature is a vertical slice. Everything it owns — entities, EF configuration, services,
settings, permissions, endpoints — lives under `src/backend/Source/Features/<Feature>/`.
`Identity`, `Notifications` and `FileManagement` are the worked examples.

## Layout

```
Features/<Feature>/
  <Feature>Feature.cs                    # IFeature.AddServices — DI registration
  Core/
    <Feature>PermissionsProvider.cs      # IPermissionDefinitionProvider (only if it owns permissions)
    <Name>Service.cs                     # interface + implementation in one file
    <Name>Setting.cs                     # options bound from configuration
    Enums.cs
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

using Backend.Attributes;
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
because this class references concrete implementations on purpose.

Options are bound here too, with validation and `ValidateOnStart()`:

```csharp
services.AddOptions<AuthSetting>()
    .Bind(configuration.GetRequiredSection("Auth"))
    .Validate(s => s.AccessTokenValidity > 0, "Authentication token lifetimes must be positive.")
    .ValidateOnStart();
```

Add the matching section to `appsettings.json` (plus `appsettings.Development.json` /
`appsettings.Testing.json` when the value differs per environment).

## 2. Services

One file per service, interface first, implementation below, both documented:

```csharp
/// <summary>Defines CRUD and lookup operations for <see cref="User"/> entities…</summary>
public interface IUserService { … }

/// <summary>EF Core-backed implementation of <see cref="IUserService"/>…</summary>
[NoDirectUse]
public class UserService(AppDbContext dbContext, IPasswordHasher passwordHasher) : IUserService { … }
```

`[NoDirectUse]` makes `Tests/Architect/NoDirectUseTests` fail if any other type depends on the
concrete class instead of the interface. Use it for services with an interface; use
`[BypassNoDirectUse]` on the rare type that legitimately needs the concrete type (feature modules,
composition roots).

## 3. Entities and DbSets

Entities live in `Core/Entities`, their EF configuration in `Core/Entities/Configuration`.
`AppDbContext.OnModelCreating` picks configurations up automatically, but the `DbSet` must be added
by hand to `src/backend/Source/Data/AppDbContext.cs`, under a comment naming the feature:

```csharp
// Notifications
public DbSet<Notification> Notifications => Set<Notification>();
```

See the `backend-entity` skill for the entity/configuration/migration details.

## 4. Permissions

If the feature guards anything, add `Core/<Feature>PermissionsProvider.cs` implementing
`IPermissionDefinitionProvider`. Providers are discovered by reflection in `Program.cs` — no manual
registration. See the `permissions` skill for the full five-file checklist.

## 5. Endpoints

One folder per area under `Endpoints/`, with a `Group` that owns the route prefix. See the
`backend-endpoint` skill.

## Feature isolation — the rule that breaks builds

`Tests/Architect/FeatureDependencyTests.Features_Should_Not_Have_Unwanted_Dependencies` scans the
compiled assembly (inheritance, fields, properties, parameters, return types **and IL bodies**) and
fails if a type under `Backend.Features.X` touches a type under `Backend.Features.Y`.

The only escape hatch is `[AllowOutside]` on the *depended-upon* type:

```csharp
/// <summary>…</summary>
[AllowOutside]
public interface ICurrentUserService { … }
```

So when a new feature needs something from `Identity`:

1. Prefer duplicating the small thing locally, or moving it to a shared root namespace
   (`Base`, `Data`, `Extensions`, `Permissions`) which is not a feature.
2. If it genuinely belongs to the other feature and is meant to be shared, mark that type
   `[AllowOutside]` and keep the surface minimal (interface + DTO, not the implementation).

`Tests/Architect/Features/FeatureA` and `FeatureB` are fixtures that prove the rule works — they
are not real features, so do not model new code on them or "fix" their intentional violations.

## Checklist

- [ ] `Features/<Feature>/<Feature>Feature.cs` with `AddServices`
- [ ] Settings bound + validated, sections added to all three `appsettings*.json`
- [ ] Entities + configurations, `DbSet` added to `AppDbContext`, migration created
- [ ] Permission provider + constants in `Permissions/Allow.cs` + mirror in `src/frontend/web/allow.ts`
- [ ] Endpoints with a group, permissions and validators
- [ ] Tests under `src/backend/Tests/Features/<Feature>/…`
- [ ] `dotnet build EasyForNet.slnx` and `dotnet test src/backend/Tests/Backend.Tests.csproj` pass
      (the architecture tests are part of that run)
