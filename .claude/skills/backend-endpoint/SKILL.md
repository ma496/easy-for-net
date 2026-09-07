---
name: backend-endpoint
description: Add or change a FastEndpoints endpoint in src/backend/Source/Features. Use when the task is "add an API endpoint", "expose X over HTTP", or when editing an existing *Endpoint.cs — covers the one-file layout (endpoint + request + validator + response + Mapperly mapper), route groups, permissions, list/paging, and error codes.
---

# Adding a backend endpoint

## Where it goes

`src/backend/Source/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Endpoint.cs`.

If the feature does not exist yet, use the `backend-feature` skill first. If `<Area>` is new,
add an `<Area>Group.cs` next to the endpoints:

```csharp
namespace Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// This route group that prefixes all user-management endpoints with the <c>users</c> segment.
/// </summary>
sealed class UsersGroup : Group
{
    public UsersGroup()
    {
        Configure("users", ep => {});
    }
}
```

The group owns the URL prefix; endpoints only declare the remainder (`Post("")`, `Put("{id}")`,
`Post("{id}/mark-as-read")`). A global route prefix comes from configuration (`RoutePrefix`), and
API versioning uses the `v` prefix — never hard-code either into a route.

## One endpoint = one file

The endpoint class, its request, its validator, its response and row DTOs, and its Mapperly
mappers all live in the same file, in that order. `Features/Identity/Endpoints/Users/UserCreateEndpoint.cs`
is the reference implementation. Skeleton:

```csharp
namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>POST /users</c> to create a new user with the supplied roles.
/// </summary>
sealed class UserCreateEndpoint(IUserService userService, AppDbContext dbContext) : Endpoint<UserCreateRequest, UserCreateResponse>
{
    public override void Configure()
    {
        Post("");
        Group<UsersGroup>();
        Permissions(Allow.User_Create);
    }

    public override async Task HandleAsync(UserCreateRequest request, CancellationToken cancellationToken)
    {
        // guard clauses first, then work, then Send.ResponseAsync
        await Send.ResponseAsync(new UserCreateResponseMapper().Map(entity), cancellation: cancellationToken);
    }
}

public sealed class UserCreateRequest { … }

sealed class UserCreateValidator : Validator<UserCreateRequest>
{
    public UserCreateValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
    }
}

public sealed class UserCreateResponse : BaseDto<Guid> { … }

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserCreateResponseMapper
{
    public partial UserCreateResponse Map(User entity);
}
```

Notes that matter:

- `HandleAsync` + `Send.ResponseAsync(...)` is the dominant style; `ExecuteAsync` returning the
  response is used only where there is nothing to send conditionally (`FileUploadEndpoint`).
- Mappers are instantiated inline (`new UserCreateRequestMapper()`), not injected.
- `RequiredMappingStrategy.Source` for request → entity (every request property must be consumed),
  `RequiredMappingStrategy.Target` for entity → response (every response property must be filled).
  Use `[MapperIgnoreSource(nameof(Req.Password))]` / `[MapProperty("UserRoles", "Roles", Use = nameof(Helper))]`
  for the mismatches, with a `private static` conversion method in the mapper.
- Request/response base classes come from `Backend.Base.Dto`: `BaseDto<TId>` (id only),
  `AuditableDto<TId>` (id + created/updated audit fields), `ListRequestDto<TId>`, `ListDto<T>`.
- Types are `sealed` and carry no accessibility modifier unless they need to be `public`
  (see the `coding-conventions` skill).

## Configure() checklist

| Need | Call |
| --- | --- |
| Route | `Post("")`, `Get("{id}")`, `Put("{id}")`, `Delete("{id}")` |
| Prefix | `Group<UsersGroup>()` — always |
| Authorization | `Permissions(Allow.X)` — the constant, never a literal |
| Signed-in but no specific permission | omit `Permissions(...)` (auth is on by default) |
| Public endpoint | `AllowAnonymous()` (signin, signup, forget/reset password, verify email) |
| Multipart upload | `AllowFileUploads()` |

Adding a new permission is a five-file change — use the `permissions` skill.

## Errors and status codes

- Business rule violations: `ThrowError("Username already exists", ErrorCodes.UsernameAlreadyExists);`
  or the property-scoped overloads (`ThrowError(x => x.Email, msg, code)`). Add new codes to
  `ErrorHandling/ErrorCodes.cs` as camelCase constants, and mirror the message key in the web
  app's `error.server.*` translations.
- Missing row: `await Send.NotFoundAsync(cancellationToken); return;`
- No current user: `await Send.UnauthorizedAsync(cancellationToken); return;`
- Never throw raw exceptions for expected failures — `ExceptionProcessor` maps unexpected ones to
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
                   new[] { "Id", "Username", "CreatedAt" }.Contains(field, StringComparer.OrdinalIgnoreCase))
    .WithMessage("The sort field is not supported.");
```

3. Build the query with `.AsNoTracking()` + `.Include(...)`, apply search/filters, take
   `CountAsync` for the total **before** paging, then `query.Process(request)` for
   sort + `IncludeIds` + paging.
4. Respond with `<Entity>ListResponse : ListDto<<Entity>ListDto>` (`Items` + `Total`).

Search uses the normalized columns: `EF.Functions.Like(x.UsernameNormalized, $"%{search}%")` with
`search = request.Search?.Trim().ToLowerInvariant()`.

## Data access

Inject the feature's service (`IUserService`, `INotificationService`) when one exists; inject
`AppDbContext` directly only for feature-local queries the service does not cover. Cross-feature
access must go through a type marked `[AllowOutside]` — `Tests/Architect/FeatureDependencyTests`
fails the build otherwise.

`AppDbContext` already stamps audit fields, runs `NormalizeProperties()`, and converts deletes of
`ISoftDelete` entities into soft deletes — do not do any of that by hand.

## Finish the change

1. Add tests — see the `backend-tests` skill. Every endpoint here has a matching
   `Tests/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Tests.cs`.
2. Add the matching RTK Query endpoint and DTOs — see the `rtk-query-api` skill.
3. If the entity/schema changed, add a migration — see the `backend-entity` skill.
