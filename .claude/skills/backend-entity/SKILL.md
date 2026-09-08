---
name: backend-entity
description: Add or change an EF Core entity — base classes, audit/soft-delete/normalized-property interfaces, IEntityTypeConfiguration, the AppDbContext DbSet, and the migration commands. Use whenever a database table or column changes.
---

# Entities, EF configuration and migrations

## Where things live

- Entity: `src/backend/Source/Features/<Feature>/Core/Entities/<Entity>.cs`
- Configuration: `…/Core/Entities/Configuration/<Entity>Configuration.cs`
- `DbSet`: `src/backend/Source/Data/AppDbContext.cs`
- Base types: `src/backend/Source/Data/Entities/Base/`

Entities that are not owned by a feature would go under `Data/Entities`, but in practice every
entity belongs to a feature.

## Base classes

Pick from `Backend.Data.Entities.Base`:

| Base | Gives you |
| --- | --- |
| `BaseEntity<TId>` | `Id` only |
| `CreatableEntity<TId>` | `Id`, `CreatedAt`, `CreatedBy` |
| `UpdatableEntity<TId>` | `Id`, `UpdatedAt`, `UpdatedBy` |
| `AuditableEntity<TId>` | both sets — the default choice |
| `AuditableEntity` (no `TId`) | audit fields for a join entity with a composite key |
| `ValueObject` | equality by components |

Opt-in interfaces:

- `ISoftDelete` (`IsDeleted`, `DeletedAt`) — `AppDbContext` installs a global query filter so
  soft-deleted rows disappear from every query, and turns `Remove(...)` into a flag update. Nothing
  else to write.
- `IHasNormalizedProperties` — implement `NormalizeProperties()` to fill `…Normalized` columns;
  `SaveChanges`/`SaveChangesAsync` calls it for added and modified entries. Normalized properties
  have a `private set` and are the columns you search and uniquely index on.

```csharp
namespace Backend.Features.Identity.Core.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// A named bundle of permissions that can be assigned to one or more users…
/// </summary>
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];

    public void NormalizeProperties()
    {
        NameNormalized = Name.Trim().ToLowerInvariant();
    }
}
```

Conventions: `Guid` keys, non-nullable reference properties initialised with `= null!`,
collections initialised with `= []`, `SystemCreated` marks seeded rows that endpoints refuse to
modify or delete.

## Configuration

One class per entity implementing `IEntityTypeConfiguration<T>`; `AppDbContext.OnModelCreating`
applies everything in the assembly, so there is nothing to register.

```csharp
namespace Backend.Features.Notifications.Core.Entities.Configuration;

using Backend.Features.Notifications.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>EF Core entity configuration for <see cref="Notification"/>…</summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>();
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.UserId);
    }
}
```

Rules the existing configurations follow:

- **Always `ToTable("<PluralName>", "<feature-schema>")`** — each feature owns a PostgreSQL schema
  (`identity`, `notifications`) named in lowercase.
- Enums are stored as strings (`HasConversion<string>()`).
- Index every column you filter or sort on. Uniqueness goes on the *normalized* column
  (`HasIndex(u => u.UsernameNormalized).IsUnique()`), while the raw column gets a non-unique index.
- Join entities get a composite key plus explicit `HasOne(...).WithMany(...).HasForeignKey(...)`,
  with `OnDelete(DeleteBehavior.Cascade)` where a child cannot outlive its parent.

## DbSet

Add to `src/backend/Source/Data/AppDbContext.cs` under the feature comment:

```csharp
// Notifications
public DbSet<Notification> Notifications => Set<Notification>();
```

## Migration

From the repository root:

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/backend/Source/Backend.csproj
dotnet ef database update --project src/backend/Source/Backend.csproj
```

The Development and Testing environments apply migrations on startup
(`Database:ApplyMigrationsOnStartup` defaults to true there only), so running the API or the test
suite is usually enough to update the local database.

Name migrations after the change (`AddNotificationGroups`), review the generated file before
committing, and never edit an already-applied migration — add a new one.

**In this template repository, `src/backend/Source/Migrations` is deliberately not copied into
generated projects** — new projects run `dotnet ef migrations add Initial` themselves. Keep that in
mind before assuming a generated project has the same migration history.

## Seeded data

`Data/DataSeeder` runs on every startup and reconciles permissions, the `Admin` role, the `admin`
user, and sample notifications. Put baseline rows a fresh database cannot work without there — not
in a migration.
