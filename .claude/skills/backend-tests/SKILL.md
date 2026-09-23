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

        var (rsp, res) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Username.Should().Be(request.Username);
    }
}
```

- Derive from `AppTestsBase(App app)`. `App` is an **assembly fixture**: one host, migrated and
  seeded once before the first test and dropped after the last. The class gets no collection of its
  own, so xunit runs it concurrently with the other test classes.
- Test method names are `Pascal_Snake` describing the case: `Valid_Input`, `Invalid_Input`,
  `List_Users_Pagination`, `Delete_Default_User`.
- Internal endpoint/request types are visible because `Meta.cs` grants
  `InternalsVisibleTo("Backend.Tests")`.
- If several tests in an area need the same setup, add an abstract
  `<Area>TestsBase : AppTestsBase` with protected helpers (see `NotificationsTestsBase`).

## What `AppTestsBase` gives you

| Member | Use |
| --- | --- |
| `Client` | this test's own typed HTTP client — its bearer token is seen by no other test |
| `Service<T>()` | resolve a service in this test's scope (`Service<IRoleService>()`) |
| `DbContext` | direct EF access for arranging/asserting state, in the same scope |
| `TenantContext` | open a tenant scope for arranging tenant-scoped rows |
| `SetAuthTokenAsync(username, password)` | sets the Bearer header; defaults to the default tenant's administrator `tenantadmin` / `Admin#123` |
| `SetPlatformAdminAuthTokenAsync()` | signs in as the platform administrator `admin`, who belongs to no tenant and so can enter none; a test that needs a platform account inside a tenant creates one with a membership (`SignInAsPlatformAdministratorEnteringAsync` in `TenancyTestsBase`) |
| `ClearAuthToken()` | test the unauthenticated path |
| `CreateAdminUserAsync(username, password)` | a fresh admin-role user (throws if it already exists) |

## Calling endpoints

Always the type-safe overloads, never raw URLs:

```csharp
var (rsp, res) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
var (rsp, res) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new() { Page = 1, PageSize = 10 });
var (rsp, res) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(request);
var (rsp, res) = await Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(new() { Id = id });
```

For a validation failure, use `ProblemDetails` as the response type and assert on the error names,
which are the camelCased request properties:

```csharp
var (rsp, res) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);
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

Test **classes** run in parallel against one database — one collection per class unless the class
says otherwise. This is what keeps the suite at around half a minute, so it is worth writing for:

- Never assert on absolute counts or "the first row" of a global list — assert
  `Should().Contain(...)` / `BeGreaterThanOrEqualTo(...)`, or filter to data the test created.
  A delta across two readings is no safer than an absolute count when what is being counted is
  something another class can add to.
- Make fixture data unique: Bogus `f.UniqueIndex` inside a `Faker<T>`, `Guid.NewGuid()` in
  title/message keys. `Faker.GlobalUniqueIndex` read directly does not make anything unique.
- Never mutate or delete the seeded `admin` or `tenantadmin` users, the `Admin` roles, or the shared test roles.
- Do not depend on ordering between test classes.
- Use `Client` and `Service<T>()`, never the fixture's own client or root provider. `App.Client` is
  a compile error for this reason; for a second identity inside one test use
  `App.CreateClient(new ClientOptions())` (see `TenancyTestsBase.ClientForAsync`).
- An override of `SetupAsync`/`TearDownAsync` must call the base implementation, which is what
  releases the test's client and scope.
- If a class genuinely shares a resource with another, give both the same `[Collection]` and say why
  in a comment. The three that exist are `Notifications` (platform-wide notices reach every
  account), `FileManagement` (one uploads directory) and `BootstrapTenant` (one tenant row).

## Why the suite is fast

Worth knowing before changing the fixtures, because each of these is load-bearing:

- The test host substitutes `TestPasswordHasher` for the production PBKDF2 hasher (`Tests/Fakes`).
  The real one costs about a quarter of a second per hash by design, and the suite signs in
  hundreds of times. `PasswordHasherTests` still pins the production parameters.
- No Hangfire worker runs under `Testing`, and `IEmailService` sends nothing.
- Under `Testing`, `Program.cs` drops the log level to `Warning` and lifts the request rate limit,
  which a suite making every request it can as one identity would otherwise trip. Both defaults are
  in code rather than in `appsettings.Testing.json`, because that file is not in source control.

To run the suite without the `dotnet test` host, execute the built runner directly — it also takes
`-parallel`, `-maxThreads` and `-filter "/*/*/ClassName/*"`:

```sh
./src/backend/Tests/bin/Debug/net10.0/Backend.Tests.exe
```

## Architecture tests

`Tests/Architect` runs with the same suite and will fail on cross-feature dependencies
(`FeatureDependencyTests`) or direct use of a `[NoDirectUse]` class (`NoDirectUseTests`).
`Tests/Architect/Features/Feature{A,B}` are deliberate violations used as fixtures — leave them
alone.
