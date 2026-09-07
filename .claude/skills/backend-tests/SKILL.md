---
name: backend-tests
description: Write or run xUnit v3 integration tests for the API under src/backend/Tests — App/AppTestsBase fixtures, typed FastEndpoints client calls, Bogus fakers, FluentAssertions, the shared seeder, and the parallel-safety rules. Use whenever an endpoint or backend service is added or changed.
---

# Backend tests

Stack: xUnit v3 + `FastEndpoints.Testing` + FluentAssertions + Bogus. Tests boot the **real** host
(`AppFixture<Program>` with environment `Testing`) against a **real PostgreSQL** matching
`appsettings.Testing.json`; the Testing environment migrates and seeds on startup.

```sh
dotnet test src/backend/Tests/Backend.Tests.csproj
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserCreateTests"
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserCreateTests.Valid_Input"
```

If the run fails at startup, the database is almost always the cause — check that PostgreSQL is up
and the connection string in `appsettings.Testing.json` resolves.

## Layout

Mirror the source tree: `src/backend/Tests/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Tests.cs`.
One test class per endpoint, named `<Entity><Action>Tests`.

## Shape of a test class

```csharp
namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Seeder;

/// <summary>
/// Tests for the <see cref="UserCreateEndpoint"/> covering validation and successful user creation.
/// </summary>
public class UserCreateTests(App app) : AppTestsBase(app)
{
    [Fact]
    public async Task Valid_Input()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex);
        var request = faker.Generate();
        request.Roles = [TestRoles.TestRoleId];

        var (rsp, res) = await App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Username.Should().Be(request.Username);
    }
}
```

- Derive from `AppTestsBase(App app)` — it joins the `SharedContext` collection, which seeds once
  per run and deletes the test database at teardown.
- Test method names are `Pascal_Snake` describing the case: `Valid_Input`, `Invalid_Input`,
  `List_Users_Pagination`, `Delete_Default_User`.
- Internal endpoint/request types are visible because `Meta.cs` grants
  `InternalsVisibleTo("Backend.Tests")`.
- If several tests in an area need the same setup, add an abstract
  `<Area>TestsBase : AppTestsBase` with protected helpers (see `NotificationsTestsBase`).

## What `AppTestsBase` gives you

| Member | Use |
| --- | --- |
| `App.Client` | typed HTTP client |
| `App.Services` | resolve services (`App.Services.GetRequiredService<IRoleService>()`) |
| `DbContext` | direct EF access for arranging/asserting state |
| `SetAuthTokenAsync(username, password)` | sets the Bearer header; defaults to `admin` / `Admin#123` |
| `ClearAuthToken()` | test the unauthenticated path |
| `CreateAdminUserAsync(username, password)` | a fresh admin-role user (throws if it already exists) |

## Calling endpoints

Always the type-safe overloads, never raw URLs:

```csharp
var (rsp, res) = await App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
var (rsp, res) = await App.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new() { Page = 1, PageSize = 10 });
var (rsp, res) = await App.Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(request);
var (rsp, res) = await App.Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(new() { Id = id });
```

For a validation failure, use `ProblemDetails` as the response type and assert on the error names,
which are the camelCased request properties:

```csharp
var (rsp, res) = await App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);
rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
res.Errors.Select(e => e.Name).Should().Equal("username", "email", "password", "firstName", "lastName", "roles");
```

## Shared seed data

`Tests/Seeder` exposes ids captured during seeding — reuse them instead of creating ad-hoc rows:

- `TestUsers.AdminUserId`, `TestUserId`, `TestOneUserId`, `TestTwoUserId`, `TestUsers.DefaultPassword` (`Test#123`)
- `TestRoles.AdminRoleId`, `TestRoleId`, `TestOneRoleId`, `TestTwoRoleId`

Each test role is granted **every** permission, so permission-denied tests need a purpose-built
role rather than one of these.

## Parallel safety

Test collections run in parallel against one database. Therefore:

- Never assert on absolute counts or "the first row" of a global list — assert
  `Should().Contain(...)` / `BeGreaterThanOrEqualTo(...)`, or filter to data the test created.
- Make fixture data unique: Bogus `f.UniqueIndex`, `Guid.NewGuid()` in title/message keys.
- Never mutate or delete the seeded `admin` user, the `Admin` role, or the shared test roles.
- Do not depend on ordering between test classes.

## Architecture tests

`Tests/Architect` runs with the same suite and will fail on cross-feature dependencies
(`FeatureDependencyTests`) or direct use of a `[NoDirectUse]` class (`NoDirectUseTests`).
`Tests/Architect/Features/Feature{A,B}` are deliberate violations used as fixtures — leave them
alone.
