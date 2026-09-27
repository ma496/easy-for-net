---
name: multi-tenancy
description: Work with tenant isolation — tenant-owned entities (IHaveTenant/IMayHaveTenant) and the AppDbContext filter and stamping, ITenantContext and its scopes, tenant vs platform scope, User.IsPlatform, memberships and seats, switching tenants, background work outside a request, and isolation tests. Use when adding data that belongs to a tenant, reading or changing the active tenant, writing code that runs without a request, or reviewing anything that could leak rows across tenants.
---

# Multi-tenancy

One database, rows tagged with a `TenantId`. Isolation is automatic *if* an entity carries a tenant
marker and code never widens a query without saying so. Everything tenancy-shaped lives in the
`Tenancy` slice (`Features/Tenancy/Core`); `ITenantContext`, `TenantContextExtensions`,
`ITenantMembershipService` and `TenantSeats` are `[AllowOutside]`, so any slice may use them.

## 1. Scopes: tenant, platform, unresolved

`ITenantContext` (`Features/Tenancy/Core/TenantContext.cs`, scoped per DI scope) is always in one of
three states:

| State | `IsResolved` | `CurrentTenantId` | Reads see | New rows get |
|---|---|---|---|---|
| Tenant | `true` | the tenant's id | that tenant's rows | that tenant |
| Platform | `true` | `null` | rows with `TenantId == null` | no tenant (`IMayHaveTenant` only) |
| Unresolved | `false` | **throws** `TenantScopeNotEstablishedException` | nothing — tenant-filtered queries throw | save throws |

`tenantContext.IsPlatformScope()` (from `TenantContextExtensions`) is the non-throwing way to ask
"resolved and no tenant". Change the scope only with the disposable handles, disposed in reverse order:
`BeginTenant(id)`, `BeginPlatformScope()`, `BeginUnscoped()`.

**In a request** `Processors/TenantContextProcessor` (a global pre-processor) opens the scope from the
session's `tenant_id` claim alone — tenant scope when it names one, platform scope when it does not. No
database read; an unauthenticated request stays unresolved. The claim is minted at sign-in, refresh,
`POST /tenants/switch` and `POST /tenants/exit`, and trusted until the token is replaced; only refresh
re-checks membership and tenant status (`TokenService.SetRenewalPrivilegesAsync`) and drops a tenant
that was suspended, deleted or left. So endpoints never set the scope — they read it:

```csharp
namespace Backend.Features.Invoices.Endpoints.Invoices;

using Backend.Features.Tenancy.Core;

sealed class InvoiceSummaryEndpoint(AppDbContext dbContext, ITenantContext tenantContext)
    : EndpointWithoutRequest<InvoiceSummaryResponse>
{
    public override void Configure()
    {
        Get("summary");
        Group<InvoicesGroup>();
        Permissions(Allow.Invoice_View);        // a Tenant-scoped permission keeps platform scope out
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (tenantContext.CurrentTenantId is not { } tenantId)
        {
            ThrowError("An active tenant is required", ErrorCodes.NoActiveTenant);
        }

        // Already restricted to the active tenant by the query filter - no Where(TenantId) needed.
        var count = await dbContext.Invoices.CountAsync(ct);
        await Send.ResponseAsync(new InvoiceSummaryResponse { TenantId = tenantId, Count = count }, cancellation: ct);
    }
}
```

**Which scope an endpoint runs in is decided by its permission's `PermissionScope`**, not by an
attribute: a `Tenant` permission is never in a platform-scope session, a `Platform` one never in a
tenant session, `Both` in either (the endpoint then answers about the platform's own rows or the
tenant's). See the `permissions` skill.

## 2. Making an entity tenant-owned

Implement one marker from `Backend.ShareData.Entities.Base`:

- `IHaveTenant` — `Guid TenantId`; every row belongs to exactly one tenant. Writing one in platform or
  unresolved scope throws.
- `IMayHaveTenant` — `Guid? TenantId`; `null` is a platform-owned row (platform roles, global
  notifications, account-owned files). Use it only when such rows genuinely exist.

```csharp
namespace Backend.Features.Invoices.Core.Entities;

using Backend.ShareData.Entities.Base;

public class Invoice : AuditableEntity<Guid>, ISoftDelete, IHaveTenant
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

`AppDbContext` then, with nothing registered by hand:

- adds a query filter named `"Tenant"` (`e.TenantId == CurrentTenantId`), read per query, beside the
  `"SoftDelete"` filter;
- on save stamps an unset `TenantId` with the active tenant, throws `TenantAttributionException` when a
  new row names a different tenant than the active one, and throws on any change to an existing row's
  `TenantId`. Never copy a tenant id from a request payload onto a row.

`Tests/Architect/TenantScopingTests` fails for any entity with neither marker unless it is listed in
`_exemptEntities` with a written reason (e.g. `UserRole` derives its tenant through `Role`). A payload
reporting the tenant implements the DTO marker that matches the entity's: `IHaveTenantDto` or
`IMayHaveTenantDto` (`Backend.Base.Dto`). The rest of entity work — configuration, `DbSet`,
migration — is the `backend-entity` skill.

**Unique indexes must lead with `TenantId`**, or one tenant's value blocks another's. For an
`IMayHaveTenant` kind add `.AreNullsDistinct(false)` so platform rows are unique among themselves too
(`RoleConfiguration`):

```csharp
builder.HasIndex(x => new { x.TenantId, x.NumberNormalized })
    .IsUnique()
    .AreNullsDistinct(false)
    .HasDatabaseName("IX_Invoices_TenantId_Number");
```

## 3. Reading across tenants — explicitly

`query.AcrossAllTenants()` (`Extensions/TenantQueryExtension.cs`) drops the `"Tenant"` filter only;
soft-delete stays. It is the sanctioned way to read across tenants, and every use must restate the
tenant it means in its own `Where`:

```csharp
var roles = await dbContext.Roles
    .AcrossAllTenants()
    .Where(role => role.TenantId == tenantId)
    .ToListAsync(ct);
```

Use it when the caller legitimately addresses a tenant other than the active one (a platform account
administering a tenant's members), before any scope exists (minting a session), or for global
maintenance (`AuthTokenCleanService`). Avoid bare `IgnoreQueryFilters()` — it also resurrects
soft-deleted rows.

## 4. Code that runs outside a request

Seeders, Hangfire jobs and startup code start **unresolved**: tenant-filtered queries and saves throw
rather than silently reading every tenant or writing an unattributed row. Pass the tenant id into the
job and open the scope yourself:

```csharp
public async Task RecalculateAsync(Guid tenantId, CancellationToken ct)
{
    using var scope = tenantContext.BeginTenant(tenantId);

    var values = await featureValueResolver.ResolveAsync(FeatureTarget.ForTenant(tenantId), ct);
    // ... tenant-filtered reads and stamped writes for this tenant only
}
```

- `DataSeeder` shows every variant: `AcrossAllTenants()` for lookups, `BeginPlatformScope()` to write
  the platform `Admin` role, `BeginTenant(TenancyConstants.BootstrapTenantId)` for the bootstrap tenant.
- Writing platform rows from inside a tenant request: wrap the add **and** the save in
  `BeginPlatformScope()` (`NotificationService.NewGlobalNotificationAsync`, account-owned uploads in
  `FileService`). Stamping happens at `SaveChangesAsync`, so save any pending tenant rows first or they
  are flushed and stamped in the wrong scope.
- `IFeatureChecker` needs a resolved scope; outside a request use `IFeatureValueResolver` with a named
  target (`feature-management` skill). Jobs over many tenants use one DI scope (and so one
  `ITenantContext`) per tenant, never one shared context across concurrent branches.

## 5. Platform accounts (`User.IsPlatform`)

`User` is global — one identity across tenants, reached through `TenantMembership` rows. `IsPlatform`
names the account's tier and travels as the `is_platform` claim (`ICurrentUserService.IsPlatform()`).
It is **not** authority: authorize on permissions. It is read only where the tier is the question —
sign-in (a platform account naming no tenant signs in with none), `HangfireAuthorizationFilter`,
`POST /tenants/exit`, `UserCreateEndpoint` (`IsPlatform = tenantContext.IsPlatformScope()`, never from
the request), which scope `GET /permissions/define` answers for, and `RoleListEndpoint` naming another
tenant from platform scope. A platform account enters only tenants it is a member of and acts there on
that tenant's roles alone.

Platform accounts are administered from platform scope only: `IUserService.TenantUsers()`, the tenant
member endpoints and role user counts exclude them unless the caller acts in platform scope. Tenant
user lists go through `TenantUsers()`, never `Users()`.

## 6. Memberships and seats

`ITenantMembershipService` owns membership and names the tenant explicitly on every call (it works from
platform scope too): `IsMemberAsync`, `AddAsync` (grants the tenant's administrator role to its first
member and enforces `Identity.MaxUserCount` under a tenant row lock), `ReplaceRolesAsync`,
`RemoveAsync` (both refuse to remove the last tenant administrator), `GetSeatsAsync` (`TenantSeats`:
`Used`, `Limit`, `IsFull`; platform accounts take no seat). A write path that creates a member itself
reserves the seat in the same transaction:

```csharp
await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
await tenantMembershipService.ReserveSeatAsync(tenantId, ct);
await userService.CreateAsync(entity, request.Password);
await transaction.CommitAsync(ct);
```

## 7. Switching and exiting

`POST /tenants/switch` (`TenantSwitchEndpoint`) requires a live membership and an active tenant, then
reissues the session via `ITenantAuthorizationService.ReissueSessionAsync(userId, tenantId)`.
`POST /tenants/exit` does the same with `null`, platform accounts only. On the web, always go through
`useTenantSwitch()` (`hooks/use-tenant-switch.ts`): it resets the whole RTK Query cache, re-reads
account info and lands on `/admin`. `lib/utils/tenant-routing.ts` decides which `/admin` screens work
without a tenant (`platformAccessiblePathPrefixes`), which are platform-only
(`platformOnlyPathPrefixes`), and where a caller with a stale or missing tenant lands
(`resolveTenantLanding` → `/select-tenant`); a new screen is tenant-only until listed there.

## 8. Testing isolation

Derive from `TenancyTestsBase` (`Tests/Features/Tenancy`) and create everything you assert on —
tests run in parallel, and seeded tenants (`TestTenants.BootstrapTenantId`, `SecondTenantId`) are
read, never written. `SetAuthTokenAsync()` is the bootstrap tenant's `tenantadmin`;
`SetPlatformAdminAuthTokenAsync()` is `admin` in platform scope. See the `backend-tests` skill.

```csharp
[Fact]
public async Task Another_Tenants_Invoice_Is_Not_Found()
{
    var home = await CreateTenantAsync();
    var other = await CreateTenantAsync();
    var roleId = await CreateTenantRoleAsync(home.Id, Allow.Invoice_View);
    var member = await CreateTenantUserAsync(home.Id, roleId);

    var foreignId = Guid.Empty;
    await TenantScopedAsync(other.Id, async () =>
    {
        var invoice = new Invoice { Number = "B-1" };
        DbContext.Invoices.Add(invoice);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreignId = invoice.Id;
    });

    await SignInAsAsync(member.Username, home.Id);
    var (rsp, _) = await Client.GETAsync<InvoiceGetEndpoint, InvoiceGetRequest, ProblemDetails>(new() { Id = foreignId });

    rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

Cover platform scope for `Both` endpoints too (`SignInAsPlatformAdministratorEnteringAsync` puts a
platform account inside a tenant); `ClientForAsync` gives a second identity its own client.

## Pitfalls

- An exemption in `TenantScopingTests` to silence a missing marker — only for kinds with no tenant.
- `AcrossAllTenants()` without a tenant predicate, or a tenant id taken from the request: a leak.
- Expecting a role, membership, suspension or plan change to bite before the next token renewal.
