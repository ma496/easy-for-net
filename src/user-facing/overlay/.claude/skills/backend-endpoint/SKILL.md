---
name: backend-endpoint
description: Add or change a FastEndpoints endpoint in src/backend/Source/Features. Use when the task is "add an API endpoint", "expose X over HTTP", or when editing an existing *Endpoint.cs — covers the one-file layout (endpoint + request + validator + response + Mapperly mapper), route groups, permissions, anonymous public endpoints, revoking sessions after an access change, system-created guards, list/paging, and error codes.
---

# Adding a backend endpoint

## Where it goes

`src/backend/Source/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Endpoint.cs`.

If the feature does not exist yet, use the `backend-feature` skill first. If `<Area>` is new,
add an `<Area>Group.cs` next to the endpoints:

```csharp
namespace Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// This route group that prefixes all role-management endpoints with the <c>roles</c> segment.
/// </summary>
sealed class RolesGroup : Group
{
    public RolesGroup()
    {
        Configure("roles", ep => {});
    }
}
```

The group owns the URL prefix; endpoints only declare the remainder (`Post("")`, `Put("{id}")`,
`Post("{id}/mark-as-read")`). A group may own a multi-segment prefix (`SeoPagesGroup` is
`seo/pages`). A global route prefix comes from configuration (`RoutePrefix`), and API versioning
uses the `v` prefix — never hard-code either into a route.

## One endpoint = one file

The endpoint class, its request, its validator, its response and row DTOs, and its Mapperly
mappers all live in the same file, in that order. `Features/Identity/Endpoints/Users/UserCreateEndpoint.cs`
is the reference implementation; `Features/Identity/Endpoints/Roles/RoleCreateEndpoint.cs` is
the short version:

```csharp
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// This endpoint that handles <c>POST /roles</c> to create a new role carrying a name no other role
/// has taken.
/// </summary>
sealed class RoleCreateEndpoint(IRoleService roleService, AppDbContext dbContext)
    : Endpoint<RoleCreateRequest, RoleCreateResponse>
{
    private const string SoftDeleteFilterKey = "SoftDelete";

    public override void Configure()
    {
        Post("");
        Group<RolesGroup>();
        Permissions(Allow.Role_Create);
    }

    public override async Task HandleAsync(RoleCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim().ToLowerInvariant();
        var nameExists = await dbContext.Roles
            .AsNoTracking()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AnyAsync(role => role.NameNormalized == normalizedName, cancellationToken);
        if (nameExists)
        {
            this.ThrowError(x => x.Name, ErrorCodes.RoleNameAlreadyExists);
        }

        var requestMapper = new RoleCreateRequestMapper();
        var entity = requestMapper.Map(request);
        await roleService.CreateAsync(entity);
        var responseMapper = new RoleCreateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>Request payload for creating a new role, supplying a name and optional description.</summary>
public sealed class RoleCreateRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>FluentValidation rules requiring a non-empty, length-bounded name.</summary>
sealed class RoleCreateValidator : Validator<RoleCreateRequest>
{
    public RoleCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(50);
        RuleFor(x => x.Description).MinimumLength(10).MaximumLength(255).When(x => !x.Description.IsNullOrEmpty());
    }
}

/// <summary>Response payload returned after a successful role creation.</summary>
public sealed class RoleCreateResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>This mapper that projects a <see cref="RoleCreateRequest"/> into a <see cref="Role"/> entity.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class RoleCreateRequestMapper
{
    public partial Role Map(RoleCreateRequest request);
}

/// <summary>This mapper that projects a <see cref="Role"/> entity into a <see cref="RoleCreateResponse"/>.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RoleCreateResponseMapper
{
    public partial RoleCreateResponse Map(Role entity);
}
```

Notes that matter:

- `HandleAsync` + `Send.ResponseAsync(...)` is the dominant style; `ExecuteAsync` returning the
  response is used only where there is nothing to send conditionally (`FileUploadEndpoint`).
- Mappers are instantiated inline (`new RoleCreateRequestMapper()`), not injected.
- `RequiredMappingStrategy.Source` for request → entity (every request property must be consumed),
  `RequiredMappingStrategy.Target` for entity → response (every response property must be filled).
  Use `[MapperIgnoreSource(nameof(UserCreateRequest.Password))]` /
  `[MapProperty("UserRoles", "Roles", Use = nameof(UserRolesToRoles))]` for the mismatches, with a
  `private static` conversion method in the mapper.
- Create and update endpoints that take the same fields share them: an abstract input class, one
  `AbstractValidator` over it pulled in with `Include(...)`, and one mapper with an `Update(input,
  entity)` method, kept in an `<Area>Contracts.cs` beside the endpoints
  (`Features/Seo/Endpoints/Pages/SeoPageContracts.cs`). Shape checks several places share are a
  static `<Feature>ValidationRules` class in the slice's `Core` (`SeoValidationRules`) — call it
  rather than restating the rule.
- DTO base classes come from `Backend.Base.Dto`: `BaseDto<TId>` (id only), `CreatableDto<TId>`,
  `UpdatableDto<TId>`, `AuditableDto<TId>` (id + audit fields), `ListRequestDto<TId>`, `ListDto<T>`.
  Add `ISystemCreatedDto` when the **projected entity** is `ISystemCreated`
  (`UserListDto : AuditableDto<Guid>, ISystemCreatedDto`).
- Types are `sealed` and carry no accessibility modifier unless they need to be `public`
  (see the `coding-conventions` skill).

## Configure() checklist

| Need | Call |
| --- | --- |
| Route | `Post("")`, `Get("{id}")`, `Put("{id}")`, `Delete("{id}")` |
| Prefix | `Group<RolesGroup>()` — always |
| Authorization | `Permissions(Allow.X)` — the constant, never a literal |
| Signed-in but no specific permission | omit `Permissions(...)` (auth is on by default) |
| Public endpoint | `AllowAnonymous()` (signin, signup, forget/reset password, verify email, `/localization/resources`, the `/seo` resolve, sitemap, robots and image endpoints) |
| Multipart upload | `AllowFileUploads()` |

Permissions are the only authorization input. An administration endpoint declares the permission
of the thing it changes (`Permissions(Allow.Seo_Update)`); `Admin.Access` is the web app's gate
on the `/admin` area and is not repeated on endpoints. Adding a permission touches several
files — use the `permissions` skill.

## Anonymous endpoints

An `AllowAnonymous()` endpoint answers anyone — a visitor, a crawler, the web server rendering a
public page — so it returns only what every visitor may see, and the same answer for all of them:

- Never read the caller from the request to decide what to return, and never return a
  `[SecretSetting]` value, an account field or an audit field.
- Anything it serves by identifier is narrowed to what is published as public.
  `SeoImageEndpoint` streams a stored file only while a live SEO page or the `Seo` setting names it
  as an image, and answers 404 for every other identifier, an existing file included — so the
  endpoint never becomes a way to read the application's files.
- Bound what an anonymous caller can make the API cache or compute: `SeoResolveEndpoint` caches
  per shipped culture, not per requested path.

## Access changes revoke sessions

A session is created with the account's roles and permissions and read on every request, so an
endpoint that changes what a session may do — an account's activity or credentials, its roles, a
role's permissions, a role's deletion — ends the affected sessions **after** the change is saved,
through `ISessionRevocationService` (`Backend.Features.Identity.Core.Sessions`):

```csharp
await userService.UpdateAsync(entity);
// Only after the change is saved, so a refused or failed update revokes nothing.
await sessionRevocationService.RevokeUserAsync(entity.Id, cancellationToken);
```

`RevokeUsersAsync(holderIds, ...)` ends every holder of a role (`ChangePermissionsEndpoint`,
`RoleDeleteEndpoint`); `RevokeUserExceptAsync(userId, currentSessionId, ...)` keeps the caller's own
session (`ChangePasswordEndpoint`). It refuses to run inside an open transaction, so commit first.
Profile fields (username, email, names) revoke nothing.

## Errors and status codes

- Business rule violations: `this.ThrowError(ErrorCodes.UsernameAlreadyExists);` or the
  property-scoped overload `this.ThrowError(x => x.Name, code)` — the call names only the
  code, never a message; add new codes to `ErrorHandling/ErrorCodes.cs` as
  `public static readonly ErrorCode` members. Called with the `this.` receiver: a bare `ThrowError(ErrorCodes.X)` never considers
  extension methods and does not compile, and `this.` binds the extension because no FastEndpoints
  instance overload accepts an `ErrorCode` — see `api-error-handling`.
- A status other than 400 goes with the code: `this.ThrowError(ErrorCodes.SeoPageNotFound, StatusCodes.Status404NotFound);`
- Missing row: `await Send.NotFoundAsync(cancellationToken); return;`
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

3. Start from the service's composable query when one exists (`userService.Users()`), else the
   `DbSet`; add `.AsNoTracking()`, apply search/filters, take `CountAsync` for the total **before**
   paging, then `query.Process(request)` for sort + `IncludeIds` + paging (the entity must be
   `IBaseEntity<TId>`), and only then project with `.Select(...)` (or a Mapperly `ProjectTo`) into
   the list DTO — no `.Include(...)`.
4. Respond with `<Entity>ListResponse : ListDto<<Entity>ListDto>` (`Items` + `Total`).

Search uses the normalized columns: `EF.Functions.Like(x.UsernameNormalized, $"%{search}%")` with
`search = request.Search?.Trim().ToLowerInvariant()`.

## Data access

Inject the feature's service (`IUserService`, `IRoleService`) when one exists; inject
`AppDbContext` directly only for feature-local queries the service does not cover. Cross-feature
access must go through a type marked `[AllowOutside]` — `Tests/Architect/FeatureDependencyTests`
fails otherwise. Several writes that must stand or fall together share
`await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);`.

`AppDbContext` already stamps audit fields, runs `NormalizeProperties()` and converts deletes of
`ISoftDelete` entities into soft deletes — do not do any of that by hand.

**Read only what you use.** A query whose rows are not modified and saved is `.AsNoTracking()` and
projects with `.Select(...)` into the response DTO, a small record or a scalar — never a whole entity
with `.Include(...)` mapped afterwards. Test existence with `AnyAsync` and count with `CountAsync`.
A query that updates an entity loads it tracked (SaveChanges is what stamps audit fields), with no
`Include` the update does not need.

**Every `Skip`/`Take` follows an `OrderBy`** that ends on a unique column (`Id`), so pages are
stable — `Process` does this for list endpoints. In Development and Testing an unordered row limit
throws (`RowLimitingOperationWithoutOrderByWarning`), so the tests catch one.

**A write that feeds a cache drops it.** A slice that caches what its anonymous endpoints serve
invalidates after every write (`seoCache.Invalidate()` after `SaveChangesAsync` in each SEO page
endpoint); the web server's own cached copy is cleared by the admin screen — see the `seo` skill.

## Finish the change

1. Add tests — see the `backend-tests` skill. Endpoints have a matching
   `Tests/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Tests.cs`.
2. Add the matching RTK Query endpoint and DTOs — see the `rtk-query-api` skill.
3. If the entity/schema changed, add a migration — see the `backend-entity` skill.
4. `dotnet build EasyForNet.slnx` and `dotnet test --project src/backend/Tests/Backend.Tests.csproj` pass.
