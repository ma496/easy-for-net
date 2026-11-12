---
name: backend-entity
description: Add or change an EF Core entity — base classes, audit/soft-delete/normalized-property/system-created interfaces, IEntityTypeConfiguration, unique indexes and how their violations are reported, the AppDbContext DbSet, seeded rows, and the migration commands. Use whenever a database table or column changes.
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
(`SeoPage.OgImageFileId` names a stored file without a `StoredFile` navigation).

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
  filtered on `IsDeleted` when a deleted row must keep reserving its name (`Role`); add
  `.HasFilter("NOT \"IsDeleted\"")` when a removed row must free the value (`SeoPage`'s
  path + culture). `.IgnoreQueryFilters(["SoftDelete"])` reads deleted rows too, for exactly that
  kind of reservation check.
- `IHasNormalizedProperties` — implement `NormalizeProperties()` to fill `…Normalized` columns
  (`private set`); the context calls it for added and modified entries. Search and unique-index on
  the normalized column.
- `ISystemCreated` (`SystemCreated`) — rows the system seeds for itself (the `admin` account and
  the `Admin` role). Nothing is enforced automatically: update/delete endpoints check the flag and
  `ThrowError` with the matching `ErrorCodes.SystemCreated…` code, and the DTOs that project the
  entity implement `ISystemCreatedDto` so the web app can hide the action.

```csharp
using Backend.ShareData.Entities.Base;

namespace Backend.Features.Identity.Core.Entities;

/// <summary>
/// A named bundle of permissions that can be assigned to one or more users, simplifying access management.
/// Roles are soft deleted so that a deleted role's name stays reserved.
/// </summary>
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties, ISoftDelete, ISystemCreated
{
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
collections with `= []`, a `/// <summary>` saying what a row is.

## Configuration

One class per entity implementing `IEntityTypeConfiguration<T>`; `OnModelCreating` applies every
configuration in the assembly, so there is nothing to register.

```csharp
using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Notifications.Core.Entities.Configuration;

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
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt })
               .IsDescending(false, false, true)
               .HasFilter("\"UserId\" IS NOT NULL AND NOT \"IsDeleted\"")
               .HasDatabaseName("IX_Notifications_Personal");
    }
}
```

Rules the existing configurations follow:

- **Always `ToTable("<PluralName>", "<feature-schema>")`** — each feature owns a PostgreSQL schema,
  its lowercase name (`identity`, `notifications`, `filemanagement`, `localization`, `settings`,
  `seo`).
- Give every string column a `HasMaxLength(...)`; a slice with many bounds keeps them as constants
  its validators share (`SeoConstants`).
- Enums declare no numeric values and are stored as strings (`HasConversion<string>()`) — an
  equality comparison in a query translates just as well against the stored name. An enum never
  sits in its entity's file: every enum of a slice lives in `Features/<Feature>/Core/Enums.cs`
  (namespace `Backend.Features.<Feature>.Core`), which holds enums and nothing else.
- Index every column you filter or sort on.
- **Uniqueness goes on the normalized column, and the raw column behind it gets a twin unique
  index on the same terms**:

```csharp
builder.HasIndex(r => r.Name)
    .IsUnique()
    .HasDatabaseName("IX_Roles_Name");
builder.HasIndex(r => r.NameNormalized)
    .IsUnique()
    .HasDatabaseName("IX_Roles_NameNormalized");
```

- A unique index over a nullable column that must treat `null` as one value (a culture-neutral
  row) uses `.AreNullsDistinct(false)` (`IX_SeoPages_Path_Culture`).
- **Name unique indexes after their columns, so the last `_` segment is the request field.**
  `ExceptionProcessor` turns a unique violation into `duplicatePropertyValue` against that segment
  with a `Normalized` suffix dropped, lower-cased, so `IX_Roles_NameNormalized` and
  `IX_Roles_Name` both report `name`.
- Relationships inside a feature get explicit `HasOne(...).WithMany(...).HasForeignKey(...)`;
  `OnDelete(DeleteBehavior.Cascade)` where a child cannot outlive its parent,
  `DeleteBehavior.Restrict` where the endpoint refuses the delete first.

## DbSet

Add to `src/backend/Source/ShareData/AppDbContext.cs` under the feature's comment:

```csharp
// Notifications
public DbSet<Notification> Notifications => Set<Notification>();
public DbSet<NotificationVisit> NotificationVisits => Set<NotificationVisit>();
public DbSet<NotificationReadCursor> NotificationReadCursors => Set<NotificationReadCursor>();
```

## Migration

From the repository root:

```sh
dotnet tool restore
npm run stop:api    # a running API locks the build output dotnet ef builds into
dotnet ef migrations add <Name> --project src/backend/Source/Backend.csproj
dotnet ef database update --project src/backend/Source/Backend.csproj
```

Development and Testing apply migrations on startup (`Database:ApplyMigrationsOnStartup` defaults
to true there only), so running the API or the test suite is usually enough locally.

Name migrations after the change (`AddNotificationGroups`), review the generated file, and never
edit an already-applied migration — add a new one. A project generated with `dotnet efn cp` starts
without a `Migrations` folder; its first migration is `dotnet ef migrations add Initial`.

## Seeded data

`ShareData/DataSeeder` runs on every startup and reconciles the permission catalogue, the
system-created `Admin` role (always holding every declared permission) and the `admin` account in
it, creates the `User` role sign-up assigns (once, with no permissions), sample notifications, and
the starter SEO pages (`/`, `/about`, `/contact`). Each step is idempotent and inserts a row only
when none exists — deleted rows included, read with `IgnoreQueryFilters()`, so a row an
administrator removed is not brought back. Put baseline rows a fresh database cannot work without
there, not in a migration.

## Verify

`npm run stop:api`, `dotnet build EasyForNet.slnx`, then `dotnet test --project src/backend/Tests/Backend.Tests.csproj` —
`FeatureDependencyTests` runs there.
