# Data model contract - Multi-tenant system

Spec: `specs/001-multi-tenant-system/spec.md` - Plan: `specs/001-multi-tenant-system/plan.md`

## What this contract covers

Every entity, property, key, index, relationship and enum the feature needs; the base class and the
audit / soft-delete / normalized-property / tenant-scope interfaces each entity uses and why; one EF
configuration class per entity; the `DbSet` lines to add to `AppDbContext`; the query filters, the
save-time attribution rules, the seeded rows, and the migration - including the parts a migration
cannot produce on its own.

**Not covered here** (owned by the other contracts, referenced only where the schema depends on it):
`Attributes/AllowNoTenantAttribute.cs`, `Processors/TenantContextProcessor.cs`, the claim pipeline
(`Helper.CreateClaims`, `ClaimConstants`, `SessionValidationMiddleware`), every endpoint, request,
validator and response - `api-contract.md`; RTK Query slices, DTO mirrors and screens -
`frontend-contract.md`; `Tests/Architect/TenantScopingTests.cs` and every other test file -
`test-plan.md` (the exemption dictionary that test asserts on is specified below, because the data
model is its authority).

Governing skills: `backend-entity` (base classes, configuration, `DbSet`, migration), `backend-feature`
(where a feature-owned entity lives, feature isolation), `coding-conventions` (file header order,
`= null!`, `[]`, XML docs, CRLF, no trailing newline in C# files).

## The model at a glance

```
tenancy.Tenants                  (new)   Tenant                - the scope itself, never tenant-scoped
tenancy.TenantMemberships        (new)   TenantMembership      - ITenantScoped, soft-deleted, xmin token
identity.Roles                   (edit)  Role                  - + TenantId, + ISoftDelete
identity.Users                   (--)    User                  - unchanged: one global account (AC-048)
identity.UserRoles               (--)    UserRole              - unchanged: tenant derived via Role.TenantId
identity.RolePermissions         (--)    RolePermission        - unchanged
identity.Permissions             (--)    Permission            - unchanged: global catalogue (AC-040)
identity.AuthTokens              (edit)  AuthToken             - + TenantId (session's active tenant)
identity.Tokens                  (--)    Token                 - unchanged: works with no tenant (AC-051)
notifications.Notifications      (edit)  Notification          - + TenantId (null = platform-wide)
notifications.NotificationVisits (--)    NotificationVisit     - unchanged: tenant derived via Notification
filemanagement.StoredFiles       (new)   StoredFile            - ITenantScoped; null tenant + owner = account-owned
```

A tenant-scoped row carries `TenantId`. Nothing else marks it, nothing else has to be edited, and the
named `"Tenant"` query filter plus the save-time attribution rules below do the rest (D4).

---

## 1. Marker interface

### `src/backend/Source/Data/Entities/Base/ITenantScoped.cs` - new

```csharp
namespace Backend.Data.Entities.Base;

/// <summary>
/// Implemented by entities whose rows belong to exactly one tenant. <see cref="AppDbContext"/>
/// registers the named "Tenant" query filter for every implementing type, attributes new rows to
/// the active tenant on save, and refuses to persist a row that carries no attribution.
/// </summary>
public interface ITenantScoped
{
    Guid? TenantId { get; set; }
}
```

- Sits beside `ISoftDelete` and `IHasNormalizedProperties` in `Backend.Data.Entities.Base` - a root
  namespace, so a feature entity implementing it costs no `[AllowOutside]` mark
  (`FeatureDependencyTester` scans `Backend.Features.*` only). (D1)
- **Nullable on purpose.** `null` is not "unattributed"; it is *platform scope*, and it means
  different things per entity, each reached by an explicit narrowed query rather than by a filter
  branch (D5): a platform role is invisible inside a tenant (AC-110, AC-121), a platform-wide
  notification is visible in every tenant (AC-054), an account-owned file belongs to a user and to no
  tenant (AC-097). Where `null` is not a legal state - `TenantMembership` - the column is made
  required in that entity's configuration rather than by a second interface.
- Serves: AC-030, AC-031, AC-033, AC-034, AC-036, AC-037, AC-080, AC-091.

---

## 2. New entities

### 2.1 `Tenant` - `src/backend/Source/Data/Entities/Tenant.cs` - new

Root namespace `Backend.Data.Entities` (D1): `AppDbContext`, `AppDbContextFactory`, `DataSeeder`, the
Hangfire jobs and `Features/Tenancy` all consume it, and every one of those except the last is a
non-feature type.

```csharp
namespace Backend.Data.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// An isolated customer of the application. Every tenant-scoped row belongs to exactly one tenant,
/// and a user reaches a tenant's data only through a <see cref="TenantMembership"/>.
/// </summary>
public class Tenant : AuditableEntity<Guid>, ISoftDelete, IHasNormalizedProperties
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; private set; } = null!;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public void NormalizeProperties()
    {
        IdentifierNormalized = Identifier.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Lifecycle state of a <see cref="Tenant"/>. Deletion is not a status: a deleted tenant is a
/// soft-deleted row (<see cref="Tenant.IsDeleted"/>).
/// </summary>
public enum TenantStatus
{
    Active = 1,
    Suspended = 2
}
```

The enum lives in the entity's own file, the way `TokenPurpose` lives in `Token.cs`.

| Choice | Why |
| --- | --- |
| `AuditableEntity<Guid>` | AC-012 and AC-078 need created-by/at and updated-by/at, populated centrally by the existing `SaveChanges` pass. `Guid` key matches every other entity here. |
| `ISoftDelete` | AC-009 and AC-079: deletion retains the row and the soft-delete filter excludes it from every query, and `Remove(...)` becomes a flag update with no endpoint code. AC-003 and AC-144 also need the identifier of a *deleted* tenant to stay reserved, which only works if the row survives. |
| `IHasNormalizedProperties` | AC-102 says the identifier is stored trimmed as entered with a normalized lower-case column beside it, "exactly as user names and role names are normalized today". `NormalizeProperties()` is called for added and modified entries by `AppDbContext`, so no endpoint sets the column. |
| **Not** `ITenantScoped` | A tenant is the scope, not something inside one. This is the first reasoned entry in the AC-091 exemption dictionary. |
| `SystemCreated` | AC-011: the bootstrap tenant refuses rename, suspend and delete - the same flag `User` and `Role` already use for seeded rows. |
| `Status` as an enum, not a bool | AC-006 and AC-008 need active/suspended, and AC-065 filters the list by lifecycle status. Stored as a string (`HasConversion<string>()`) like `NotificationType`. `Deleted` is deliberately absent - it would be a second source of truth beside `IsDeleted`. |
| No `Memberships` navigation | Memberships are always reached through `dbContext.TenantMemberships`, which the `"Tenant"` filter already restricts; a navigation would be an easy way to load another tenant's rows through an `Include` and buys nothing. |

Serves AC-001, AC-002, AC-005, AC-006, AC-008, AC-009, AC-011, AC-012, AC-065, AC-078, AC-079,
AC-100..AC-102, AC-144.

### 2.2 `TenantMembership` - `src/backend/Source/Data/Entities/TenantMembership.cs` - new

```csharp
namespace Backend.Data.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// The link that makes a user account a member of a tenant. A membership carries no state of its
/// own: it exists, or it has been removed. The roles the member holds inside the tenant are the
/// user's role assignments whose role belongs to that tenant.
/// </summary>
public class TenantMembership : AuditableEntity<Guid>, ISoftDelete, ITenantScoped
{
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

| Choice | Why |
| --- | --- |
| `AuditableEntity<Guid>` | AC-078 records who added a member and when, and who last changed the row. A surrogate `Guid` key rather than a composite `(TenantId, UserId)` key because AC-106 lets the same pair exist several times over a tenant's life - once per removal-and-re-add cycle. |
| `ISoftDelete` | AC-018 and AC-079: removing a member retains the row. AC-107 then requires removed rows to be *excluded* from the duplicate check, which the soft-delete filter does for free and the filtered unique index below enforces at the database. |
| `ITenantScoped` | AC-031 restricts the member list to the active tenant like any other tenant-scoped read. |
| No `User` navigation, no FK to `identity.Users` | D3: `Features/Identity` must not become a cross-feature surface, and a bare `Guid` reference is already how this codebase models it - migration `RemoveUserDependencyFromNotification` deliberately dropped the same relationship from `Notification.UserId`. Existence of the account is checked in the endpoint (AC-016). |
| No `Tenant` navigation, no FK to `tenancy.Tenants` | Uniform with every other `TenantId` column (section 5): `TenantId` is a discriminator, so implementing `ITenantScoped` stays a one-line change with nothing else to remember. Tenants are never hard-deleted (hard deletion is out of scope), so a referential constraint would never fire. |
| `xmin` concurrency token (configuration, not a CLR property) | AC-081 and D14. `builder.UseXminAsConcurrencyToken()` maps PostgreSQL's system column as a shadow property, so the entity stays clean; the losing writer of two concurrent role-assignment replacements gets `DbUpdateConcurrencyException`. A test that needs the value reads it with `dbContext.Entry(membership).Property("xmin")`. |
| No `Status`, no `IsActive` | AC-103 defines an active membership as exactly "row exists, not soft-deleted, tenant neither suspended nor deleted" and says the system carries no further per-tenant membership state. Adding a flag would contradict the criterion. |

Serves AC-013..AC-021, AC-078, AC-079, AC-081, AC-103, AC-106, AC-107.

### 2.3 `StoredFile` - `src/backend/Source/Features/FileManagement/Core/Entities/StoredFile.cs` - new

Feature-owned (`backend-feature`: an entity a feature owns lives in that feature's `Core/Entities`).
This is the first entity `FileManagement` has, so it also introduces the feature's schema.

```csharp
namespace Backend.Features.FileManagement.Core.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// Persisted record of one uploaded file. A tenant-scoped file carries the tenant it was uploaded
/// in; an account-owned file, such as a profile image, carries no tenant and instead names the
/// account that owns it.
/// </summary>
public class StoredFile : AuditableEntity<Guid>, ITenantScoped
{
    public Guid? TenantId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string FileName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}
```

| Choice | Why |
| --- | --- |
| The entity exists at all | AC-058 and AC-067 require `crossTenantFileAccess` to be distinguishable from "no such file". Only a row can tell "belongs to another tenant" from "does not exist" (D17). `IStorageProvider`, `LocalStorageProvider.GetSafePath` and the generated stored file name are untouched, so `User.Image` keeps storing exactly what it stores today. |
| `AuditableEntity<Guid>` | Uploads are attributable (who, when) like every other row; the `Guid` key is separate from `FileName`, which is the storage-level identity. |
| `ITenantScoped` | AC-057 attributes an upload to the tenant active at the time; AC-058 and AC-059 then fall out of the `"Tenant"` filter, with the endpoint turning "present but filtered out" into the defined error code. |
| `TenantId == null` **and** `OwnerUserId != null` = account-owned | AC-097: a profile image belongs to an account and to no tenant, and stays readable while acting in any tenant or in none. Reached by `AcrossAllTenants().Where(f => f.TenantId == null && f.OwnerUserId == userId)` (D5). |
| **Not** `ISoftDelete` | Deleting a file deletes the blob, which is irreversible, so a retained row would point at nothing. No criterion asks for file retention - AC-079 names tenants and memberships only - and after deletion "not found" is the correct answer. |
| `OriginalFileName`, `ContentType` | Recorded on upload so a download returns the name and MIME type that were uploaded rather than re-deriving the type from the extension, which is all `FileService.GetContentType` can do today. No size and no checksum: quotas are out of scope. |

Serves AC-057..AC-060, AC-097..AC-099, AC-128.

---

## 3. Changed entities

### 3.1 `Role` - `src/backend/Source/Features/Identity/Core/Entities/Role.cs`

Add, keeping everything already there:

```csharp
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties, ISoftDelete, ITenantScoped
{
    public Guid? TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    // existing: SystemCreated, Name, NameNormalized, Description, UserRoles, RolePermissions
}
```

- `ITenantScoped` - AC-038 scopes roles to one tenant; AC-110..AC-112 then need no per-query
  predicate. `TenantId == null` is platform scope, which after seeding is exactly the `Admin` role and
  nothing else (AC-121, AC-083, AC-145).
- `ISoftDelete` - **new deletion semantics for an existing entity.** AC-039, AC-144 and AC-147 require
  a deleted role's name to stay reserved inside its tenant; roles are hard-deleted today
  (`RoleService.DeleteAsync` calls `Remove`), which would release the name. With `ISoftDelete`,
  `AppDbContext.ApplySoftDeleteRules` converts that same `Remove` into a flag update with no service
  change, and the non-filtered unique index below keeps the name reserved.
  Consequences to carry into the affected tasks: `DataSeeder`'s reconciliation must not resurrect or
  duplicate a soft-deleted role, and `RoleDeleteTests` / `RoleCreateTests` assert hard deletion today.
- Serves AC-038, AC-039, AC-042, AC-043, AC-110..AC-113, AC-121, AC-144, AC-147.

### 3.2 `Notification` - `src/backend/Source/Features/Notifications/Core/Entities/Notification.cs`

```csharp
public class Notification : AuditableEntity<Guid>, ISoftDelete, ITenantScoped
{
    public Guid? TenantId { get; set; }
    // existing: Type, TitleKey, MessageKey, IsRead, Group, Metadata, IsDeleted, DeletedAt, UserId, Visits
}
```

- `TenantId` set = the notification belongs to that tenant; combined with the existing `UserId` this
  gives the two addressing modes AC-053 asks for - `UserId` set is one member of the tenant, `UserId`
  null is every member of the tenant.
- `TenantId == null` **with** `UserId == null` = platform-wide, the mode that already exists and that
  AC-054 preserves; it stays distinguishable precisely because the tenant column is null.
- The `"Tenant"` filter alone would hide platform-wide rows inside a tenant, so the list and
  unread-count endpoints union them back in through
  `AcrossAllTenants().Where(n => n.TenantId == null)` (D5). That is an endpoint concern; the schema's
  part is only the nullable column and the index below.
- Serves AC-052..AC-056, AC-129, AC-130.

### 3.3 `AuthToken` - `src/backend/Source/Features/Identity/Core/Entities/AuthToken.cs`

```csharp
public Guid? TenantId { get; set; }
```

- The active tenant persisted on the refresh-token row, so `TokenService.SetRenewalPrivilegesAsync`,
  which has only a `UserId` to rebuild claims from, re-issues the tenant the session was carrying
  instead of dropping it or carrying the previous one (AC-108, AC-109, AC-139).
- **Deliberately not `ITenantScoped`.** A refresh is authenticated but has no established tenant yet;
  a filtered `AuthTokens` set would make the row unreadable at exactly the moment it is needed.
  Recorded as a reasoned exemption in section 8.
- Nullable, because a session may legitimately carry no tenant (AC-140: a user who belongs to two
  tenants has none until they choose).

### 3.4 Entities deliberately unchanged

`User` - AC-048 keeps sign-in identifiers globally unique, so the unique indexes on
`UsernameNormalized` and `EmailNormalized` stay exactly as they are. `Permission` - AC-040 keeps the
catalogue global and code-declared, and D16 puts the platform/tenant distinction on the **in-memory**
permission definition types, never in a column, so `Permission`, `DataSeeder`'s reconciliation loop
and the schema are all untouched. `UserRole`, `RolePermission`, `Token`, `NotificationVisit` - each
appears in the exemption dictionary in section 8 with its reason.

---

## 4. EF configuration classes

One class per entity implementing `IEntityTypeConfiguration<T>`; `AppDbContext.OnModelCreating`
applies everything in the assembly, so none of these is registered by hand. Every table is
`ToTable("<Plural>", "<lowercase feature schema>")`.

### 4.1 `src/backend/Source/Data/Entities/Configuration/TenantConfiguration.cs` - new

Namespace `Backend.Data.Entities.Configuration` - a new folder, since `Data/Entities` currently holds
only `Base`.

```csharp
builder.ToTable("Tenants", "tenancy");

builder.Property(x => x.Status).HasConversion<string>();

builder.HasIndex(x => x.IdentifierNormalized)
       .IsUnique()
       .HasDatabaseName("IX_Tenants_Identifier");   // see the naming note below
builder.HasIndex(x => x.Identifier).IsUnique(false);
builder.HasIndex(x => x.Name).IsUnique(false);
builder.HasIndex(x => x.Status);
builder.HasIndex(x => x.CreatedAt);
```

- **The unique index is not filtered.** AC-003 compares against soft-deleted tenants too, and AC-144
  requires that comparison to be a database constraint over retained rows - so no `HasFilter`. A
  deleted tenant's identifier is reserved permanently.
- **The database name is load-bearing.** `ExceptionProcessor` reports the offending field as
  `constraintName.Split('_').Last().ToLowerInvariant()`. `IX_Tenants_Identifier` yields `identifier`,
  which is the request field name (AC-003, AC-068); the EF default `IX_Tenants_IdentifierNormalized`
  would yield `identifiernormalized`, which no form knows.
- The `Status` and `CreatedAt` indexes serve the list filter and the default sort (AC-062, AC-065);
  `Name` and `Identifier` serve free-text search (AC-064) and sorting.

### 4.2 `src/backend/Source/Data/Entities/Configuration/TenantMembershipConfiguration.cs` - new

```csharp
builder.ToTable("TenantMemberships", "tenancy");

builder.Property(x => x.TenantId).IsRequired();      // null is not a legal membership scope (AC-080)
builder.UseXminAsConcurrencyToken();                 // AC-081, D14

builder.HasIndex(x => new { x.TenantId, x.UserId })
       .IsUnique()
       .HasFilter("\"IsDeleted\" = false")
       .HasDatabaseName("IX_TenantMemberships_TenantId_User");
builder.HasIndex(x => x.UserId);
builder.HasIndex(x => x.CreatedAt);
```

- **This unique index *is* filtered, and that is not a contradiction.** AC-106 and AC-107 require a
  removed membership to be re-creatable and excluded from the duplicate comparison, so uniqueness must
  cover live rows only. The non-filtered rule in AC-144 and the "Query cost" note is about the
  uniqueness that *becomes per-tenant* - tenant identifier and tenant role name - where deletion must
  not release the name.
- The database name ends in `User`, so a constraint violation reports the field `user`, the root of
  the `userId` request field (AC-068). The endpoint checks for the duplicate first and raises
  `duplicateTenantMembership` (AC-015); the index is the race backstop.
- `UserId` alone is indexed because the hottest query in the system asks it: "which tenants does this
  account belong to", run at sign-in (AC-024), on every tenant switch (AC-025) and on every request by
  the session-plus-tenant middleware (AC-108, AC-116).

### 4.3 `src/backend/Source/Features/Identity/Core/Entities/Configuration/RoleConfiguration.cs` - edit

```csharp
builder.ToTable("Roles", "identity");

builder.HasIndex(r => r.Name).IsUnique(false);       // unchanged

// removed: builder.HasIndex(r => r.NameNormalized).IsUnique();
builder.HasIndex(r => new { r.TenantId, r.NameNormalized })
       .IsUnique()
       .AreNullsDistinct(false)                      // PostgreSQL 15+ NULLS NOT DISTINCT
       .HasDatabaseName("IX_Roles_TenantId_Name");
```

- Per-tenant uniqueness (AC-039), covering soft-deleted roles because the index is not filtered
  (AC-144, AC-147).
- **`AreNullsDistinct(false)` is mandatory, not a refinement.** PostgreSQL treats every `NULL` as
  distinct in a unique index by default, so without it platform roles (`TenantId == null`) would have
  no name uniqueness at all and two `Admin` platform roles could coexist. It requires PostgreSQL 15+;
  record that in the migration comment. If the Npgsql 10 provider does not expose `AreNullsDistinct`,
  create the index with `migrationBuilder.Sql(...)` instead and keep the same database name.
- The database name ends in `Name`, so the reported field is `name`, matching the request field behind
  `roleNameAlreadyExists` (AC-068).
- The composite's leading column also serves every "roles of the active tenant" query (AC-110).

### 4.4 `src/backend/Source/Features/Notifications/Core/Entities/Configuration/NotificationConfiguration.cs` - edit

```csharp
// removed: builder.HasIndex(x => x.UserId);
builder.HasIndex(x => new { x.TenantId, x.UserId });
// unchanged: the TitleKey, MessageKey and CreatedAt indexes and the Type string conversion
```

Every notification read is "the active tenant, or platform-wide, for this user", so the composite is
the index that query wants and the single-column one becomes dead weight (AC-052, AC-055, AC-056).

### 4.5 `src/backend/Source/Features/FileManagement/Core/Entities/Configuration/StoredFileConfiguration.cs` - new

```csharp
builder.ToTable("StoredFiles", "filemanagement");

builder.HasIndex(x => x.FileName).IsUnique().HasDatabaseName("IX_StoredFiles_FileName");
builder.HasIndex(x => x.TenantId);
builder.HasIndex(x => x.OwnerUserId);
```

- `FileName` is unique because it is the identity the client holds and the only key a download request
  supplies; AC-059 depends on one stored name resolving to exactly one row and therefore to exactly
  one tenant.
- New schema `filemanagement`, the lowercase feature name, matching `identity` and `notifications`.

### 4.6 `AuthTokenConfiguration` - unchanged

`TenantId` needs no index: `AuthToken` rows are only ever looked up by refresh-token value or by
`UserId`, never by tenant.

---

## 5. Relationships

| Relationship | Declared how | Why |
| --- | --- | --- |
| `TenantMembership.TenantId` -> `Tenant` | **No FK, no navigation.** Column plus index only. | Uniform with every other `TenantId` column, which is what makes `ITenantScoped` a one-line opt-in with nothing else to remember (D4). Tenants are never hard-deleted, so the constraint could never fire. The tenant's existence is enforced where it is decided - the endpoint (AC-010) and the pre-processor. |
| `TenantMembership.UserId` -> `User` | **No FK, no navigation.** Column plus index only. | D3: keeps `User` out of any cross-feature surface. Precedent in this codebase: migration `RemoveUserDependencyFromNotification` removed exactly this relationship from `Notification.UserId`. Account existence is checked in the endpoint (AC-016). |
| `Role.TenantId`, `Notification.TenantId`, `StoredFile.TenantId`, `AuthToken.TenantId` | Column plus index only. | Same rule: a feature entity must never need a line of tenancy configuration beyond implementing the interface. |
| `StoredFile.OwnerUserId` -> `User` | Column plus index only. | Same reasoning; `FileManagement` must not depend on `Features/Identity`. |
| `UserRole` -> `User`, `Role` | Unchanged (`HasKey(new { UserId, RoleId })`, both `HasOne(...).WithMany(...)`). | D12: a member's roles inside a tenant are the existing `UserRole` rows whose `Role.TenantId` is that tenant. No new join table and no `TenantId` on `UserRole` (D13). |
| `RolePermission`, `NotificationVisit`, `AuthToken`/`Token` -> `User` | Unchanged. | Nothing about tenancy changes them. |

**Consequence to keep in mind:** because `UserRole` carries no tenant, "the roles this member holds in
this tenant" is always `userRoles.Where(ur => ur.Role.TenantId == tenantId)` - and when the role is the
platform `Admin` role, `TenantId` is null and the assignment is a platform grant (AC-041, AC-083).
Deleting a tenant does not delete its roles' assignments; the soft-deleted tenant makes them
unreachable, which is what AC-009 asks for.

---

## 6. `AppDbContext` - `src/backend/Source/Data/AppDbContext.cs`

### 6.1 DbSets

Add, keeping the existing feature comments:

```csharp
// Tenancy
public DbSet<Tenant> Tenants => Set<Tenant>();
public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

// FileManagement
public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
```

The `// Tenancy` block goes first, above `// Identity`, because it is the kernel every other set is
filtered against; `// FileManagement` goes last, after `// Notifications`. `using
Backend.Data.Entities;` and `using Backend.Features.FileManagement.Core.Entities;` join the existing
using block.

### 6.2 Constructor and the current-tenant member

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options,
                          ICurrentUserService currentUserService,
                          ITenantContext tenantContext)
    : DbContext(options)
{
    /// <summary>The tenant the "Tenant" query filter restricts to, re-read on every query.</summary>
    public Guid? CurrentTenantId => tenantContext?.CurrentTenantId;
```

Exposed as a property, not captured into the filter expression as a value, so the filter re-evaluates
per query instead of baking one tenant into the compiled model (D4). The null-conditional is the
design-time belt-and-braces of D7.

### 6.3 Query filters

`SoftDeleteFilter` is **renamed in place, not replaced**: the same model-walk loop now registers the
filter under the key `"SoftDelete"`, and a sibling `TenantFilter` registers `"Tenant"`.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    SoftDeleteFilter(modelBuilder);   // now: HasQueryFilter("SoftDelete", filter)
    TenantFilter(modelBuilder);       //      HasQueryFilter("Tenant", e => e.TenantId == CurrentTenantId)
}
```

- `TenantFilter` is an **instance** method - `SoftDeleteFilter` is `static` today and stays static -
  because the expression must reference context state:
  `Expression.Equal(Expression.Property(e, nameof(ITenantScoped.TenantId)), Expression.Property(Expression.Constant(this), nameof(CurrentTenantId)))`,
  which is the documented EF way to have the value parameterized per query rather than baked in.
- Two named filters coexist and either can be suppressed alone (EF Core 10 with Npgsql 10). That is the
  whole of AC-034 and AC-036, and it is the one assumption wave 1 of the plan exists to prove against
  a real database before anything is built on it.
- **The only sanctioned suppression is `AcrossAllTenants()`** (section 7). A raw
  `IgnoreQueryFilters(["Tenant"])` call site anywhere else is a review failure (AC-036).

### 6.4 Save-time rules

The existing `SaveChanges` / `SaveChangesAsync` pass already calls `ApplySoftDeleteRules()`,
`NormalizeProperties()` and stamps the audit fields. Tenant attribution joins that same pass - in both
methods, since they duplicate the loop today - as `ApplyTenantRules()`, called after
`NormalizeProperties()` and before the audit loop:

| Entry state | Context state | Rule | Criterion |
| --- | --- | --- | --- |
| `Added`, `ITenantScoped`, `TenantId == null` | resolved to a tenant | stamp `TenantId = CurrentTenantId` | AC-030 |
| `Added`, `ITenantScoped`, `TenantId == null` | explicit platform scope | leave `null` - the only way to write a platform-scoped row | AC-054, AC-097, AC-121 |
| `Added`, `ITenantScoped`, `TenantId == null` | unresolved | throw `TenantScopeNotEstablishedException` | AC-035, AC-037, AC-080, AC-131 |
| `Added`, `ITenantScoped`, `TenantId != null` | resolved to a *different* tenant | throw `TenantAttributionException` | AC-030, AC-032 |
| `Modified`, `ITenantScoped` | any | if the `TenantId` original value differs from the current value, throw `TenantAttributionException` | AC-030, AC-032, AC-080 |

Attribution is never taken from the request payload - the caller cannot supply a tenant (AC-030,
AC-138). The two exception types are new, one file each beside the existing `UserIdNullException`:

- `src/backend/Source/Exceptions/TenantScopeNotEstablishedException.cs`
- `src/backend/Source/Exceptions/TenantAttributionException.cs`

Both are programming errors rather than user errors, so they surface through `ExceptionProcessor` as
`internalServerError`. A caller who simply has no tenant established is refused far earlier, by the
pre-processor, with `noActiveTenant` (api-contract).

### 6.5 What reads do when nothing is established

`CurrentTenantId` returning `null` while unresolved would silently turn every tenant-scoped read into
"platform rows only", and AC-037 and AC-131 both refuse that. So the requirement the data layer places
on the kernel is: **`ITenantContext.CurrentTenantId` throws `TenantScopeNotEstablishedException` when
the context is unresolved**, so a background job, a seeder pass or any caller that forgot to open a
scope fails loudly on its first tenant-scoped query instead of quietly reading nothing. A query that
calls `AcrossAllTenants()` removes the filter and never touches the property, so a deliberate
cross-tenant read still works while unresolved. `BeginPlatformScope()` is *resolved with a null
tenant* and reads platform rows - a different state from unresolved, which is exactly D6's point.

Callers this obliges (each is a task elsewhere, listed so nobody is surprised): `DataSeeder` - opens
`BeginTenant(TenancyConstants.BootstrapTenantId)` for the admin membership and reads across tenants
for permission reconciliation; the Hangfire recurring jobs; the tests' non-HTTP fixture; and the
session-plus-tenant middleware, which reads `TenantMemberships` *before* a tenant is established and
therefore must use `AcrossAllTenants()`.

---

## 7. `AcrossAllTenants()` - `src/backend/Source/Extensions/TenantQueryExtension.cs` - new

```csharp
namespace Backend.Extensions;

/// <summary>
/// Query extensions that relax tenant restriction explicitly and by name. This is the only
/// sanctioned way to read across tenants; the soft-delete filter stays in force.
/// </summary>
public static class TenantQueryExtension
{
    /// <summary>
    /// Suppresses the named "Tenant" query filter for this query only, leaving every other filter -
    /// including soft delete - applied.
    /// </summary>
    public static IQueryable<T> AcrossAllTenants<T>(this IQueryable<T> query) where T : class
        => query.IgnoreQueryFilters(["Tenant"]);
}
```

`Backend.Extensions` is already a global using in `Meta.cs`, so no file has to import it. Relaxing
tenancy alone and never soft delete is AC-036 and AC-034 in one line, and "explicitly and by name"
becomes one grep.

Legitimate call sites this feature creates, each narrowing immediately (D5): the sign-in and
tenant-switch membership lookups (AC-024, AC-025), the session-plus-tenant middleware, the platform
administrator's tenant, member, user and role lists (AC-046, AC-095, AC-113), the platform-wide
notification union (AC-054), the account-owned file lookup (AC-097), and `DataSeeder`.

---

## 8. The AC-091 exemption dictionary

`Tests/Architect/TenantScopingTests` (owned by `test-plan.md`) walks `AppDbContext.Model` and fails the
build unless every entity type either carries a query filter named `"Tenant"` or appears in a
`Dictionary<Type, string>` of **written reasons** - D13: a reason string, never a bare `Type[]`, so an
exemption cannot be added by a one-word edit nobody reviews. The data model is the authority on that
dictionary's contents; here it is in full:

| Type | Reason |
| --- | --- |
| `Tenant` | Is the scope itself. |
| `User` | A global account: one identity across every tenant (AC-048). Tenant reach is the membership row. |
| `UserRole` | Tenant derived through `Role.TenantId`; a column here would duplicate that fact and admit rows where the two disagree (D13). |
| `Permission` | Global, code-declared catalogue, identical for every tenant and reconciled from code on every start (AC-040). |
| `RolePermission` | Tenant derived through `Role.TenantId`. |
| `AuthToken` | Session record read during refresh, before any tenant is established; it *carries* the active tenant rather than being scoped by it. |
| `Token` | Email-verification and password-reset tokens back account self-service flows that must work with no tenant (AC-051). |
| `NotificationVisit` | Tenant derived through `Notification.TenantId`. |

`Role`, `Notification`, `TenantMembership` and `StoredFile` are **not** in the table - they are
`ITenantScoped` and must carry the filter. Anything added later that is neither `ITenantScoped` nor
listed above fails the build, which is the criterion (AC-091).

---

## 9. Constants and seeded rows

### 9.1 `src/backend/Source/Tenancy/TenancyConstants.cs` - new

```csharp
namespace Backend.Tenancy;

/// <summary>Fixed identifiers shared by the migration, the data seeder and the tests.</summary>
public static class TenancyConstants
{
    /// <summary>
    /// Identity of the system-created bootstrap tenant. Fixed so the migration, the seeder and the
    /// tests all name the same row instead of re-deriving it.
    /// </summary>
    public static readonly Guid BootstrapTenantId = new("00000000-0000-0000-0000-000000000001");

    public const string BootstrapTenantName = "Default";
    public const string BootstrapTenantIdentifier = "default";
}
```

`static readonly`, not `const`: C# has no `Guid` constant. The plan's wording ("a compile-time `Guid`
constant") is met in substance - one fixed value, compiled in, shared by both writers. The name and
identifier are deliberately generic: this ships to every generated project, so no customer name may
appear (spec "Genericity").

### 9.2 `src/backend/Source/Data/DataSeeder.cs` - edit

Per the `backend-entity` skill, baseline rows a fresh database cannot work without belong here rather
than in a migration - and they *must* be here, because a generated project has no migration history to
carry them (AC-085). Reconcile idempotently on every start:

1. **The bootstrap tenant.** Look it up with `Tenants.AcrossAllTenants().FirstOrDefault(t => t.Id == BootstrapTenantId)`
   or insert `{ Id = BootstrapTenantId, SystemCreated = true, Name = "Default", Identifier = "default",
   Status = Active }` (AC-082).
2. **The permission catalogue** reconciliation stays exactly as it is - it touches `Permissions` and
   `RolePermissions` only, so it never deletes a tenant, a membership or a tenant-scoped role (AC-084).
3. **The `Admin` role stays platform-scoped** (`TenantId == null`) and keeps every permission,
   including the platform ones (AC-083). It must be looked up with `AcrossAllTenants()`, since the
   seeder runs outside a tenant.
4. **The bootstrap tenant's system-created administrator role** - a tenant role
   (`TenantId = BootstrapTenantId`, `SystemCreated = true`) holding every **non-platform** permission
   (AC-042), assigned to the seeded `admin` account.
5. **The seeded `admin` account's membership** in the bootstrap tenant, written inside
   `BeginTenant(BootstrapTenantId)` (AC-082).
6. **Delete the `Public` role and its assignments, idempotently** (AC-145, AC-121). The seeder creates
   that role today, and after this feature every role must either belong to a tenant or carry platform
   scope. Removing the creation is not enough - existing databases have the row.
7. **The sample notifications** keep their existing shape; the user-targeted ones are written inside
   the bootstrap tenant scope and the one global row stays platform-wide (`TenantId == null`, AC-054).

Because the seeder is idempotent and complete, a freshly generated project reaches the same state from
`dotnet ef migrations add Initial` plus a first run that this repository reaches from the migration
below (AC-085).

### 9.3 `src/backend/Source/Data/AppDbContextFactory.cs` - edit

`return new AppDbContext(optionsBuilder.Options, null!)` becomes a real construction with design-time
no-op `ICurrentUserService` and `ITenantContext` implementations declared in that same file (D7).
`dotnet ef migrations add Initial` is literally the first command a generated project runs, and the
model now contains a filter that reads the tenant accessor - a null there would break scaffolding for
every generated project. Nothing else in the file changes.

---

## 10. Migration

### 10.1 The command

From the repository root, against a running PostgreSQL 15+ matching
`src/backend/Source/appsettings.Development.json`:

```sh
dotnet tool restore
dotnet ef migrations add AddMultiTenancy --project src/backend/Source/Backend.csproj
dotnet ef database update --project src/backend/Source/Backend.csproj
```

Migration name: **`AddMultiTenancy`** - named after the change, as the skill requires. Review the
generated file before committing; never edit an already-applied migration.

### 10.2 What the scaffolder produces on its own

Create the `tenancy` and `filemanagement` schemas; create `tenancy.Tenants`,
`tenancy.TenantMemberships` and `filemanagement.StoredFiles` with their indexes; add nullable
`TenantId` to `identity.Roles`, `identity.AuthTokens` and `notifications.Notifications`; add
`IsDeleted` and `DeletedAt` to `identity.Roles`; drop `IX_Roles_NameNormalized` and
`IX_Notifications_UserId`; create `IX_Roles_TenantId_Name` and `IX_Notifications_TenantId_UserId`.

### 10.3 What a migration cannot do automatically - hand-written, in this order inside `Up()`

The generated file is edited so the data steps sit between the schema steps and no intermediate state
is invalid:

1. **`NULLS NOT DISTINCT`.** If `.AreNullsDistinct(false)` does not round-trip through the Npgsql 10
   provider, drop the scaffolded `IX_Roles_TenantId_Name` creation and issue
   `migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Roles_TenantId_Name\" ON identity.\"Roles\" (\"TenantId\", \"NameNormalized\") NULLS NOT DISTINCT;")`.
   Add a comment recording the PostgreSQL 15+ minimum. Without this, platform role names are not
   unique at all (D11).
2. **Insert the bootstrap tenant** with `TenancyConstants.BootstrapTenantId`, `SystemCreated = true`,
   `Name = 'Default'`, `Identifier = 'default'`, `IdentifierNormalized = 'default'`,
   `Status = 'Active'`, `IsDeleted = false`, `CreatedAt = now()`. The fixed id is why the seeder
   recognises this row instead of creating a second one.
3. **Backfill `identity.Roles`**: every role except `Admin` gets `TenantId = <bootstrap>`; `Admin`
   stays `NULL` (platform scope, AC-083). `IsDeleted = false` for every existing row.
4. **Delete the `Public` role**: its `identity.UserRoles` rows first, then its
   `identity.RolePermissions` rows, then the role itself (AC-145, AC-121).
5. **Backfill `tenancy.TenantMemberships`**: one row per existing `identity.Users` row -
   `gen_random_uuid()`, `TenantId = <bootstrap>`, `CreatedAt = now()`, `IsDeleted = false`.
6. **Backfill `notifications.Notifications`**: rows with `"UserId" IS NOT NULL` get the bootstrap
   tenant; rows with `"UserId" IS NULL` keep `NULL` and stay platform-wide (AC-054).
7. **Backfill `filemanagement.StoredFiles` from existing avatars**: one account-owned row per non-null
   `identity.Users."Image"` - `TenantId = NULL`, `OwnerUserId = <user>`, `FileName = "Image"`,
   `OriginalFileName = "Image"`, a content type derived from the extension - so existing profile
   images keep resolving once uploads and downloads require a row (AC-097, AC-128).

**No column added to an existing table is non-nullable.** `Roles.TenantId`, `Notifications.TenantId`
and `AuthTokens.TenantId` are nullable by design, since null is platform scope or no tenant, and
`Roles.IsDeleted` is added with `defaultValue: false`. The one required tenant column,
`TenantMemberships.TenantId`, is on a table this migration creates, so it is `NOT NULL` from the start
and nothing has to be tightened afterwards. That is deliberate: the migration has no "add nullable,
backfill, alter to not null" step that could strand a half-migrated database.

`Down()` reverses the schema only. Say so in a comment: the deleted `Public` role, the backfilled
attributions and the generated membership and file rows are data, and are not restored.

### 10.4 Generated projects

`src/backend/Source/Migrations/` is **not** copied by `dotnet efn cp`. A newly generated project runs
`dotnet ef migrations add Initial` and gets the complete tenancy schema in one step from the model
described above, with no migration history and nothing to backfill; the seeder (section 9.2) supplies
the bootstrap tenant, the platform `Admin` role, the tenant administrator role and the admin
membership on first start (AC-085). Everything in section 10.3 exists for this template repository's
own development databases, so nobody has to hand-roll an `UPDATE` against them.

---

## 11. Index inventory

| Table | Index | Unique | Filtered | Serves |
| --- | --- | --- | --- | --- |
| `tenancy.Tenants` | `IX_Tenants_Identifier` (`IdentifierNormalized`) | yes | no - covers soft-deleted rows | AC-003, AC-102, AC-144, AC-147 |
| `tenancy.Tenants` | `Identifier`, `Name` | no | no | AC-062, AC-064 |
| `tenancy.Tenants` | `Status` | no | no | AC-065 |
| `tenancy.Tenants` | `CreatedAt` | no | no | AC-062 |
| `tenancy.TenantMemberships` | `IX_TenantMemberships_TenantId_User` (`TenantId`, `UserId`) | yes | `IsDeleted = false` | AC-015, AC-106, AC-107 |
| `tenancy.TenantMemberships` | `UserId` | no | no | AC-024, AC-025, AC-108, AC-116 |
| `tenancy.TenantMemberships` | `CreatedAt` | no | no | AC-061, AC-062 |
| `identity.Roles` | `IX_Roles_TenantId_Name` (`TenantId`, `NameNormalized`), `NULLS NOT DISTINCT` | yes | no | AC-039, AC-110, AC-144, AC-147 |
| `identity.Roles` | `Name` | no | no | existing search and sort |
| `notifications.Notifications` | (`TenantId`, `UserId`) | no | no | AC-052, AC-055, AC-056 |
| `filemanagement.StoredFiles` | `IX_StoredFiles_FileName` | yes | no | AC-058, AC-059 |
| `filemanagement.StoredFiles` | `TenantId`, `OwnerUserId` | no | no | AC-057, AC-097 |

Every column used for tenant restriction, filtering or sorting is indexed, and restriction is applied
in the query by the filter rather than in memory - the "Query cost" non-functional requirement.
`IsDeleted` is not indexed anywhere, matching the existing configurations.

---

## 12. Traceability

| Criterion | Element |
| --- | --- |
| AC-001, AC-005, AC-012 | `Tenant : AuditableEntity<Guid>` with `Name`, `Identifier`, `Status` |
| AC-002, AC-006, AC-008 | `TenantStatus` enum, string-converted |
| AC-003, AC-144, AC-147 | non-filtered unique `IX_Tenants_Identifier` on `IdentifierNormalized` |
| AC-009, AC-079 | `Tenant : ISoftDelete`, `TenantMembership : ISoftDelete`, the named `"SoftDelete"` filter |
| AC-011, AC-082 | `Tenant.SystemCreated`, `TenancyConstants.BootstrapTenantId`, seeder step 1 |
| AC-013, AC-014, AC-018 | `TenantMembership` row per (tenant, user), soft-deleted on removal |
| AC-015, AC-107 | filtered unique `IX_TenantMemberships_TenantId_User` |
| AC-016, AC-021 | bare `UserId` with no FK; existence and authority checked in the endpoint |
| AC-030, AC-080 | `ApplyTenantRules()` in both `SaveChanges` overloads; `TenantMemberships.TenantId` required |
| AC-031..AC-034 | the named `"Tenant"` filter registered beside the named `"SoftDelete"` filter |
| AC-035, AC-037, AC-131 | `CurrentTenantId` throws while unresolved; an unattributed insert throws |
| AC-036 | `AcrossAllTenants()`, the only sanctioned suppression |
| AC-038, AC-039, AC-110..AC-112 | `Role.TenantId` plus the composite unique index |
| AC-041, AC-083, AC-121 | `Role.TenantId == null` is platform scope; only the seeded `Admin` role holds it |
| AC-042 | the seeder provisions the bootstrap tenant's system-created administrator role |
| AC-052..AC-056 | `Notification.TenantId`; null tenant with null `UserId` is platform-wide; `(TenantId, UserId)` index |
| AC-057..AC-060, AC-097..AC-099 | `StoredFile` with `TenantId` / `OwnerUserId` and a unique `FileName` |
| AC-061..AC-066 | indexes on `Name`, `Identifier`, `Status`, `CreatedAt`, `UserId` |
| AC-078 | `AuditableEntity<Guid>` on `Tenant` and `TenantMembership`; `Role` already has it |
| AC-081 | `UseXminAsConcurrencyToken()` on `TenantMembership` |
| AC-085 | migrations are not shipped; the seeder is the complete first-run path |
| AC-091 | `ITenantScoped` plus the reasoned exemption dictionary in section 8 |
| AC-102 | `Tenant : IHasNormalizedProperties`, `IdentifierNormalized` with a `private set` |
| AC-103 | membership carries no state beyond existence and the soft-delete flag |
| AC-106 | surrogate `Guid` key, so a re-add is a new row |
| AC-108, AC-109, AC-139 | `AuthToken.TenantId` |
| AC-145 | the seeder deletes the `Public` role; migration step 4 removes it from existing databases |

---

## 13. Notes for the other contracts

- `Tenant` is **not** `ITenantScoped`, so `dbContext.Tenants` returns every live tenant regardless of
  who is asking. AC-066 - a non-platform caller sees only their own tenants - is therefore an explicit
  join to `TenantMemberships` in `TenantListEndpoint`. It is not free, and it is the one place the
  filter does not cover.
- The member list, the tenant switch and the session-plus-tenant middleware all read
  `TenantMemberships` **before** a tenant is established, and must use `AcrossAllTenants()` narrowed by
  `UserId`.
- Writing a platform-scoped row - a platform-wide notification, an account-owned profile image - is
  only possible inside an explicit platform scope; anywhere else the save-time rules either stamp the
  active tenant or throw.
- `Role` becoming `ISoftDelete` changes the behaviour of existing code and tests: `RoleService.DeleteAsync`,
  `DataSeeder`'s reconciliation (which must not resurrect a soft-deleted role), `RoleDeleteTests` and
  `RoleCreateTests`.
- Requiring a `StoredFile` row for every served file changes the Testing-environment seed: seeded
  profile images need account-owned rows, or `ImagePreview` breaks in the test suite.

## 14. Open questions

None that block implementation. Two decisions were taken here rather than deferred, and are flagged so
a reviewer can overrule them cheaply:

- **`filemanagement` as the schema name** for `StoredFiles` - the lowercase feature name, matching
  `identity` and `notifications`; `file_management` would be the only snake_case schema in the
  database.
- **`StoredFile` is hard-deleted, not soft-deleted** - no criterion asks for file retention, and the
  blob is gone either way, so a retained row would point at nothing.
