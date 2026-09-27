---
name: backend-entity
description: Add or change an EF Core entity — base classes, audit/soft-delete/normalized-property/system-created interfaces, the tenant markers (IMayHaveTenant/IHaveTenant) and the tenant-scoping architecture test, IEntityTypeConfiguration, the AppDbContext DbSet, and the migration commands. Use whenever a database table or column changes.
---

# Entities, EF configuration and migrations

## Where things live

- Entity: `src/backend/Source/Features/<Feature>/Core/Entities/<Entity>.cs`
- Configuration: `…/Core/Entities/Configuration/<Entity>Configuration.cs`
- `DbSet`: `src/backend/Source/ShareData/AppDbContext.cs`
- Base types and marker interfaces: `src/backend/Source/ShareData/Entities/Base/`
  (namespace `Backend.ShareData.Entities.Base` — not a global using, so entity files import it)

Every entity belongs to a feature. An entity is never reachable from another feature — other
slices go through a service interface marked `[AllowOutside]` (see `backend-feature`), and a
foreign key to another feature's row is a plain `Guid` column with no navigation property
(`Role.TenantId` names a tenant without a `Tenant` navigation).

## Base classes

| Base | Gives you |
| --- | --- |
| `BaseEntity<TId>` | `Id` only |
| `CreatableEntity<TId>` | `Id`, `CreatedAt`, `CreatedBy` |
| `UpdatableEntity<TId>` | `Id`, `UpdatedAt`, `UpdatedBy` |
| `AuditableEntity<TId>` | both sets — the default choice |
| `AuditableEntity` (no `TId`) | audit fields for a join entity with a composite key (`UserRole`, `RolePermission`) |
| `ValueObject` | equality by components |

`AppDbContext.SaveChanges`/`SaveChangesAsync` stamp the audit fields from `ICurrentUserService` —
never set them by hand. `IQueryableExtension.Process` needs `IBaseEntity<TId>` and orders by
`UpdatedAt`/`CreatedAt` when no sort field is given.

## Opt-in interfaces

- `ISoftDelete` (`IsDeleted`, `DeletedAt`) — a named `SoftDelete` query filter hides deleted rows
  from every query, and `Remove(...)` becomes a flag update. A unique index is deliberately *not*
  filtered on `IsDeleted` when a deleted row must keep reserving its name (`Role`, `Edition`,
  `Tenant`); add `.HasFilter("\"IsDeleted\" = false")` when a removed row must free the value
  (`TenantMembership`).
- `IHasNormalizedProperties` — implement `NormalizeProperties()` to fill `…Normalized` columns
  (`private set`); the context calls it for added and modified entries. Search and unique-index on
  the normalized column.
- `ISystemCreated` (`SystemCreated`) — rows the system seeds for itself (the administrator
  accounts and roles, the bootstrap tenant). Nothing is enforced automatically: update/delete
  endpoints check the flag and `ThrowError` with the matching `ErrorCodes.SystemCreated…` code, and
  the DTOs that project the entity implement `ISystemCreatedDto` so the web app can hide the action.
- **Tenant markers** — see below.

### Tenant ownership

Decide for every new entity whether its rows belong to a tenant:

| Marker | `TenantId` | Use when |
| --- | --- | --- |
| `IMayHaveTenant` | `Guid?` — `null` is platform scope | a kind has rows that legitimately belong to no tenant (`Role`, `Notification`, `StoredFile`) |
| `IHaveTenant` | `Guid` | every row must name a tenant — an unattributed row cannot be written, not even in platform scope |

Implementing either marker is all it takes. `AppDbContext` then:

- registers the named `Tenant` query filter (`e.TenantId == CurrentTenantId`), so every query is
  restricted to the active tenant — including `ExecuteUpdate`/`ExecuteDelete`;
- on save, stamps a new row with the active tenant, throws `TenantScopeNotEstablishedException`
  when no scope is established, throws `TenantAttributionException` for a row naming another
  tenant, and refuses any change to an existing row's `TenantId`.

So never copy a `TenantId` from a request, and never add `Where(x => x.TenantId == …)` for the
active tenant. `.AcrossAllTenants()` (in `Backend.Extensions`) relaxes the tenant filter alone for
a deliberate cross-tenant read, keeping soft delete in force. Writing outside a request (seeder,
background job) needs a scope opened first — `using (tenantContext.BeginTenant(id))` or
`BeginPlatformScope()`. The `multi-tenancy` skill covers scopes in depth.

**An entity with neither marker fails the build's tests.** `Tests/Architect/TenantScopingTests`
requires every persisted entity to be tenant-scoped or listed in its `_exemptEntities` dictionary
with a written reason. Exempt a kind only when it truly belongs to no tenant (`Tenant`, `Edition`,
`User`, `Permission`) or its tenant is derived through a parent (`UserRole` via `Role`,
`NotificationVisit` via `Notification`) — a second tenant column there would admit rows where the
two disagree.

```csharp
namespace Backend.Features.Identity.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// A named bundle of permissions that can be assigned to one or more users. A role that names a
/// tenant is that tenant's alone, while a role with no <see cref="TenantId"/> is platform scoped.
/// </summary>
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties, ISoftDelete, IMayHaveTenant, ISystemCreated
{
    public Guid? TenantId { get; set; }
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public void NormalizeProperties()
    {
        NameNormalized = Name.Trim().ToLowerInvariant();
    }
}
```

Conventions: `Guid` keys, non-nullable reference properties initialised with `= null!`,
collections with `= []`, a `/// <summary>` saying what a row is and which tenant it belongs to.

## Configuration

One class per entity implementing `IEntityTypeConfiguration<T>`; `OnModelCreating` applies every
configuration in the assembly, so there is nothing to register.

```csharp
namespace Backend.Features.Notifications.Core.Entities.Configuration;

using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Notification"/>. Maps it to the
/// "Notifications" table in the "notifications" schema and indexes the columns it is read by.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>();
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}
```

Rules the existing configurations follow:

- **Always `ToTable("<PluralName>", "<feature-schema>")`** — each feature owns a PostgreSQL schema,
  its lowercase name (`identity`, `notifications`, `tenancy`, `filemanagement`).
- Enums are stored as strings (`HasConversion<string>()`), unless a query must compare them
  numerically (`Permission.Scope` uses `HasConversion<int>()`).
- Index every column you filter or sort on, `TenantId` included (leading a composite index when
  reads are "X of the active tenant").
- **Uniqueness goes on the normalized column, scoped by tenant for tenant-owned kinds**:

```csharp
builder.HasIndex(r => new { r.TenantId, r.NameNormalized })
    .IsUnique()
    .AreNullsDistinct(false)              // platform rows (null tenant) are unique among themselves too
    .HasDatabaseName("IX_Roles_TenantId_Name");
```

- **Name unique indexes so the last `_` segment is the request field.** `ExceptionProcessor`
  turns a unique violation into `duplicatePropertyValue` against that segment lower-cased, so
  `IX_Editions_Name` reports `name` where the EF default `IX_Editions_NameNormalized` would report a
  field no form has. The raw column gets a separate non-unique index.
- Relationships inside a feature get explicit `HasOne(...).WithMany(...).HasForeignKey(...)`;
  `OnDelete(DeleteBehavior.Cascade)` where a child cannot outlive its parent,
  `DeleteBehavior.Restrict` where the endpoint refuses the delete first.

## DbSet

Add to `src/backend/Source/ShareData/AppDbContext.cs` under the feature's comment:

```csharp
// Notifications
public DbSet<Notification> Notifications => Set<Notification>();
public DbSet<NotificationVisit> NotificationVisits => Set<NotificationVisit>();
```

## Migration

From the repository root:

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/backend/Source/Backend.csproj
dotnet ef database update --project src/backend/Source/Backend.csproj
```

Development and Testing apply migrations on startup (`Database:ApplyMigrationsOnStartup` defaults
to true there only), so running the API or the test suite is usually enough locally.

Name migrations after the change (`AddNotificationGroups`), review the generated file, and never
edit an already-applied migration — add a new one. A project generated with `dotnet efn cp` starts
without a `Migrations` folder; its first migration is `dotnet ef migrations add Initial`.

## Seeded data

`ShareData/DataSeeder` runs on every startup and reconciles the permission catalogue, the bootstrap
tenant, the platform administrator role and `admin` account, the bootstrap tenant's administrator
role and `tenantadmin` account with its membership, sample notifications, and prunes stored feature
values the code no longer declares. Put baseline rows a fresh database cannot work without there,
inside the tenant or platform scope they belong to — not in a migration.

## Verify

`dotnet build EasyForNet.slnx`, then `dotnet test src/backend/Tests/Backend.Tests.csproj` —
`TenantScopingTests` and `FeatureDependencyTests` run there.
