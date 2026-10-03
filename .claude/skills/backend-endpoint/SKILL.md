---
name: backend-endpoint
description: Add or change a FastEndpoints endpoint in src/backend/Source/Features. Use when the task is "add an API endpoint", "expose X over HTTP", or when editing an existing *Endpoint.cs — covers the one-file layout (endpoint + request + validator + response + Mapperly mapper), route groups, permissions, tenant scope and plan checks inside a handler, system-created guards, list/paging, and error codes.
---

# Adding a backend endpoint

## Where it goes

`src/backend/Source/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Endpoint.cs`.

If the feature does not exist yet, use the `backend-feature` skill first. If `<Area>` is new,
add an `<Area>Group.cs` next to the endpoints:

```csharp
namespace Backend.Features.Tenancy.Endpoints.Editions;

/// <summary>
/// This route group that prefixes all edition administration endpoints with the <c>editions</c> segment.
/// </summary>
sealed class EditionsGroup : Group
{
    public EditionsGroup()
    {
        Configure("editions", ep => {});
    }
}
```

The group owns the URL prefix; endpoints only declare the remainder (`Post("")`, `Put("{id}")`,
`Post("{id}/mark-as-read")`). A global route prefix comes from configuration (`RoutePrefix`), and
API versioning uses the `v` prefix — never hard-code either into a route.

## One endpoint = one file

The endpoint class, its request, its validator, its response and row DTOs, and its Mapperly
mappers all live in the same file, in that order. `Features/Identity/Endpoints/Users/UserCreateEndpoint.cs`
is the reference implementation; `Features/Tenancy/Endpoints/Editions/EditionCreateEndpoint.cs` is
the short version:

```csharp
namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// This endpoint that handles <c>POST /editions</c> to create a plan the platform can put tenants on.
/// </summary>
sealed class EditionCreateEndpoint(IEditionService editionService) : Endpoint<EditionCreateRequest, EditionCreateResponse>
{
    public override void Configure()
    {
        Post("");
        Group<EditionsGroup>();
        Permissions(Allow.Edition_Create);
    }

    public override async Task HandleAsync(EditionCreateRequest request, CancellationToken cancellationToken)
    {
        if (await editionService.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            this.ThrowError(x => x.Name, ErrorCodes.EditionNameAlreadyExists);
        }

        var requestMapper = new EditionCreateRequestMapper();
        var entity = await editionService.CreateAsync(requestMapper.Map(request), cancellationToken);

        var responseMapper = new EditionCreateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>Request payload for creating an edition.</summary>
public sealed class EditionCreateRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>FluentValidation rules ensuring a create-edition request supplies a usable plan name.</summary>
sealed class EditionCreateValidator : Validator<EditionCreateRequest>
{
    public EditionCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().EditionName();
        RuleFor(x => x.Description).EditionDescription();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Response payload returned after a successful edition creation.</summary>
public sealed class EditionCreateResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>This mapper that projects an <see cref="EditionCreateRequest"/> into an <see cref="Edition"/>.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class EditionCreateRequestMapper
{
    public partial Edition Map(EditionCreateRequest request);
}

/// <summary>This mapper that projects a created <see cref="Edition"/> into an <see cref="EditionCreateResponse"/>.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EditionCreateResponseMapper
{
    public partial EditionCreateResponse Map(Edition entity);
}
```

Notes that matter:

- `HandleAsync` + `Send.ResponseAsync(...)` is the dominant style; `ExecuteAsync` returning the
  response is used only where there is nothing to send conditionally (`FileUploadEndpoint`).
- Mappers are instantiated inline (`new EditionCreateRequestMapper()`), not injected.
- `RequiredMappingStrategy.Source` for request → entity (every request property must be consumed),
  `RequiredMappingStrategy.Target` for entity → response (every response property must be filled).
  Use `[MapperIgnoreSource(nameof(UserCreateRequest.Password))]` /
  `[MapProperty("UserRoles", "Roles", Use = nameof(UserRolesToRoles))]` for the mismatches, with a
  `private static` conversion method in the mapper.
- Field rules shared with other endpoints come from the slice's `<Entity>ValidationRules`
  extensions (`.EditionName()`, `.TenantIdentifier()`), always after `NotEmpty()` — do not restate them.
- DTO base classes come from `Backend.Base.Dto`: `BaseDto<TId>` (id only), `CreatableDto<TId>`,
  `UpdatableDto<TId>`, `AuditableDto<TId>` (id + audit fields), `ListRequestDto<TId>`, `ListDto<T>`.
  Add the marker interfaces the **projected entity** implements, with the matching property:
  `ISystemCreatedDto` for an `ISystemCreated` entity (`UserListDto : AuditableDto<Guid>, ISystemCreatedDto`),
  `IMayHaveTenantDto` / `IHaveTenantDto` when a DTO of an `IMayHaveTenant` / `IHaveTenant` entity
  reports its `TenantId`.
- Types are `sealed` and carry no accessibility modifier unless they need to be `public`
  (see the `coding-conventions` skill).

## Configure() checklist

| Need | Call |
| --- | --- |
| Route | `Post("")`, `Get("{id}")`, `Put("{id}")`, `Delete("{id}")` |
| Prefix | `Group<EditionsGroup>()` — always |
| Authorization | `Permissions(Allow.X)` — the constant, never a literal |
| Signed-in but no specific permission | omit `Permissions(...)` (auth is on by default) |
| Public endpoint | `AllowAnonymous()` (signin, signup, forget/reset password, verify email) |
| Multipart upload | `AllowFileUploads()` |

Permissions are the only authorization input. A tenant-only operation is kept out of platform
scope by giving its permission `PermissionScope.Tenant`, and a plan-gated one by
`.RequireFeatures(...)` on the permission — there is no endpoint attribute for either. Adding a
permission touches several files — use the `permissions` skill.

## Tenant scope inside a handler

`TenantContextProcessor` establishes the tenant from the session before the handler runs, and
`AppDbContext` filters and attributes every `IMayHaveTenant`/`IHaveTenant` set to it. So:

- Query tenant-scoped sets directly — a row of another tenant is simply not found, and reporting it
  exactly like a missing row (`Send.NotFoundAsync`) is the intended behaviour.
- Never accept a `TenantId` in a request or add `Where(x => x.TenantId == …)` for the active tenant.
- Inject `ITenantContext` (from `Backend.Features.Tenancy.Core`) when behaviour depends on the
  scope: `tenantContext.CurrentTenantId is { } tenantId`, `tenantContext.IsPlatformScope()`.
- `.AcrossAllTenants()` only for a deliberate cross-tenant read, with an explicit tenant predicate.
- An `AllowAnonymous()` endpoint has no scope; one that touches tenant-scoped data opens it itself:
  `using (tenantContext.BeginPlatformScope()) { … }` (`SignupEndpoint`).

The `multi-tenancy` skill covers scopes, memberships and switching.

## Plan (entitlement) checks

An endpoint gated on a feature alone, with no permission to hang it on, checks in its handler:

```csharp
await featureChecker.CheckEnabledAsync(FeatureNames.FileManagement_Enabled, ct);
var maxFileSizeMb = await featureChecker.GetAsync(FeatureNames.FileManagement_MaxFileSizeMb, long.MaxValue / BytesPerMegabyte, ct);
```

`IFeatureChecker` needs a tenant scope; `ExceptionProcessor` answers a disabled feature with 403
`featureDisabled` and an exceeded limit with 403 `featureLimitExceeded`. See the `feature-management` skill.

## Errors and status codes

- Business rule violations: `this.ThrowError(ErrorCodes.UsernameAlreadyExists);` or the
  property-scoped overload `this.ThrowError(x => x.Name, code)` — the call names only the
  code, never a message; add new codes to `ErrorHandling/ErrorCodes.cs` as
  `public static readonly ErrorCode` members. Called with the `this.` receiver: a bare `ThrowError(ErrorCodes.X)` never considers
  extension methods and does not compile, and `this.` binds the extension because no FastEndpoints
  instance overload accepts an `ErrorCode` — see `api-error-handling`.
- Missing row (or another tenant's row): `await Send.NotFoundAsync(cancellationToken); return;`
- No current user: `await Send.UnauthorizedAsync(cancellationToken); return;`
- System-created rows refuse update/delete after the lookup:

```csharp
if (entity.SystemCreated)
    this.ThrowError(ErrorCodes.SystemCreatedRoleCannotBeDeleted);
```

- A unique-index violation that races past the endpoint's own check becomes
  `duplicatePropertyValue` in `ExceptionProcessor`; still check first so the caller gets the
  specific code. Never throw raw exceptions for expected failures — unexpected ones map to
  `internalServerError`.

A new error code also needs a translation on the web side; the `api-error-handling` skill covers the
round trip.

## List endpoints

Follow `UserListEndpoint`:

1. Request extends `ListRequestDto<Guid>` and adds only the extra filters.
2. Validator does `Include(new ListRequestDtoValidator<Guid>());` **and whitelists sortable fields**
   — `Process(...)` throws for a field that is not a sortable property, so the whitelist is what
   turns a bad `sortField` into a 400:

```csharp
RuleFor(request => request.SortField)
    .Must(field => string.IsNullOrWhiteSpace(field) ||
                   new[] { "Id", "Username", "Email", "CreatedAt", "UpdatedAt" }
                       .Contains(field, StringComparer.OrdinalIgnoreCase))
    .WithMessage("The sort field is not supported.");
```

3. Start from the service's composable query when it defines who may be listed
   (`userService.TenantUsers()`), else the `DbSet`; add `.AsNoTracking()`, apply search/filters,
   take `CountAsync` for the total **before** paging, then `query.Process(request)` for sort +
   `IncludeIds` + paging (the entity must be `IBaseEntity<TId>`), and only then project with
   `.Select(...)` (or a Mapperly `ProjectTo`) into the list DTO — no `.Include(...)`.
4. Respond with `<Entity>ListResponse : ListDto<<Entity>ListDto>` (`Items` + `Total`).

Search uses the normalized columns: `EF.Functions.Like(x.UsernameNormalized, $"%{search}%")` with
`search = request.Search?.Trim().ToLowerInvariant()`.

## Data access

Inject the feature's service (`IUserService`, `IEditionService`) when one exists; inject
`AppDbContext` directly only for feature-local queries the service does not cover. Cross-feature
access must go through a type marked `[AllowOutside]` — `Tests/Architect/FeatureDependencyTests`
fails otherwise. Several writes that must stand or fall together share
`await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);`.

`AppDbContext` already stamps audit fields, runs `NormalizeProperties()`, attributes tenant-scoped
rows, and converts deletes of `ISoftDelete` entities into soft deletes — do not do any of that by hand.

**Read only what you use.** A query whose rows are not modified and saved is `.AsNoTracking()` and
projects with `.Select(...)` into the response DTO, a small record or a scalar — never a whole entity
with `.Include(...)` mapped afterwards. Test existence with `AnyAsync` and count with `CountAsync`.
A query that updates an entity loads it tracked (SaveChanges is what stamps audit fields), with no
`Include` the update does not need.

**Every `Skip`/`Take` follows an `OrderBy`** that ends on a unique column (`Id`), so pages are
stable — `Process` does this for list endpoints. In Development and Testing an unordered row limit
throws (`RowLimitingOperationWithoutOrderByWarning`), so the tests catch one.

## Finish the change

1. Add tests — see the `backend-tests` skill. Endpoints have a matching
   `Tests/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Tests.cs`.
2. Add the matching RTK Query endpoint and DTOs — see the `rtk-query-api` skill.
3. If the entity/schema changed, add a migration — see the `backend-entity` skill.
4. `dotnet build EasyForNet.slnx` and `dotnet test src/backend/Tests/Backend.Tests.csproj` pass.
