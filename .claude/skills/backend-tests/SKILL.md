---
name: backend-tests
description: Write or run xUnit v3 integration tests for the API under src/backend/Tests — App/AppTestsBase fixtures, typed FastEndpoints client calls, Bogus fakers, FluentAssertions, the shared seeder and tenants, tenant-aware base classes, and the parallel-safety rules. Use whenever an endpoint or backend service is added or changed.
---

# Backend tests

Stack: xUnit v3 + `FastEndpoints.Testing` + FluentAssertions + Bogus. Tests boot the **real** host
(`AppFixture<Program>` with environment `Testing`) against a **real PostgreSQL** matching
`appsettings.Testing.json`; the Testing environment migrates on startup, `App.SetupAsync` runs
`TestsDataSeeder`, and `App.TearDownAsync` drops the database at the end of the run.

```sh
dotnet test src/backend/Tests/Backend.Tests.csproj
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserCreateTests"
dotnet test src/backend/Tests/Backend.Tests.csproj --filter "FullyQualifiedName~UserCreateTests.Valid_Input"
```

If the run fails at startup, the database is almost always the cause — check that PostgreSQL is up
and the connection string in `appsettings.Testing.json` resolves.

## Layout

Mirror the source tree: `src/backend/Tests/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Tests.cs`,
and `Features/<Feature>/Core/...` for service tests. One test class per endpoint, named
`<Entity><Action>Tests`. Cross-cutting suites sit at the root (`Architect/`, `ErrorHandling/`,
`FeatureManagement/`, `Middleware/`).

## Shape of a test class

```csharp
namespace Backend.Tests.Features.Identity.Endpoints.Users;

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
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var request = faker.Generate();
        request.Roles = [TestRoles.TestRoleId];

        var (rsp, res) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Username.Should().Be(request.Username);
    }
}
```

- Derive from `AppTestsBase(App app)` — or from a suite base built on it (below). `App` is an
  **assembly fixture** (`Tests/Meta.cs`): one host, migrated and seeded once before the first test
  and dropped after the last. The class gets no collection of its own, so xunit runs it concurrently
  with the other test classes.
- `Tests/Meta.cs` already global-imports FluentAssertions, xUnit, Bogus, FastEndpoints(.Testing),
  EF Core, `Backend.ErrorHandling`, `Backend.Permissions`, `Backend.Tests.Seeder`,
  `Backend.Features.Tenancy.Core.FeatureManagement` and the rest — do not re-add them per file.
- Test method names are `Pascal_Snake` describing the case: `Valid_Input`, `Invalid_Input`,
  `List_Users_Pagination`, `Delete_Default_User`.
- Internal endpoint/request types are visible because the API's `Meta.cs` grants
  `InternalsVisibleTo("Backend.Tests")`.
- Pass `TestContext.Current.CancellationToken` to any async EF/service call a test makes directly.

## Base classes

| Base | Adds |
| --- | --- |
| `AppTestsBase` | client, scope, sign-in, `TenantContext` (table below) |
| `TenancyTestsBase` (`Tests/Features/Tenancy`) | `CreateTenantAsync`, `CreateTenantRoleAsync(tenantId, params permissions)`, `CreateTenantUserAsync(tenantId, params roleIds)`, `CreateAccountWithoutMembershipAsync`, `SignInAsAsync(username, tenantId?)`, `ClientForAsync(username, tenantId?)`, `TenantScopedAsync`, `SessionForAsync` (a renewable session), `SignInAsPlatformAdministratorEnteringAsync`, `NewTenantIdentifier()` |
| `FeatureTestsBase` (`Tests/FeatureManagement`) | editions and stored feature values: `CreateEditionAsync`, `PutOnEditionAsync`, `CreateTenantOnEditionAsync`, `SetForTenantAsync`, `SetForEditionAsync`, `ResolveForTenantAsync` |
| `SettingsTestsBase` (`Tests/Features/Settings`) | the `Settings` collection and its layers: `SetPlatformValuesAsync`, `SetTenantValuesAsync`, `TenantClientAsync`, `PlatformClientAsync`, `StoredValuesAsync`, and a teardown that removes every platform row (see the `settings` skill) |
| `NotificationsTestsBase`, `FileTestsBase` | the arrangements those suites repeat, and their `[Collection]` |

Anything that needs its own tenant, role or account should derive from `TenancyTestsBase` and create
it rather than reuse a seeded one. If several tests in a new area need the same setup, add an
abstract `<Area>TestsBase` on top of the closest of these.

## What `AppTestsBase` gives you

| Member | Use |
| --- | --- |
| `Client` | this test's own typed HTTP client — its bearer token is seen by no other test |
| `Service<T>()` | resolve a service in this test's scope (`Service<IRoleService>()`) |
| `DbContext` | direct EF access for arranging/asserting state, in the same scope |
| `TenantContext` | this scope's `ITenantContext`; `using var _ = TenantContext.BeginTenant(tenantId);` before writing tenant-scoped rows through `DbContext` (save-time attribution refuses them otherwise); read across tenants with `.AcrossAllTenants()` |
| `SetAuthTokenAsync(username, password, tenantId?)` | sets the Bearer header; defaults to the default tenant's administrator `tenantadmin` / `Admin#123`, acting in that tenant |
| `SetPlatformAdminAuthTokenAsync()` | signs in as the platform administrator `admin`, who holds no membership and so acts in no tenant; a platform account inside a tenant is created with a membership (`SignInAsPlatformAdministratorEnteringAsync`) |
| `SwitchTenantAsync(tenantId)` | re-mints the signed-in caller's session in another tenant it belongs to |
| `MarkAsPlatformAccountAsync(userId)` | sets `User.IsPlatform` on an account the test created |
| `ClearAuthToken()` | test the unauthenticated path |
| `CreateAdminUserAsync(username, password)` | a fresh administrator of the default tenant (throws if it already exists) |

A token holds only the account and `sid`; roles, permissions and the tenant live in the session the
store holds, which every request reads (`SessionOfAsync(accessToken)` returns it). A change made
through an endpoint that revokes — a user's roles, a role's grants, a membership, a tenant's status or
plan — ends the affected sessions at once, so assert that the **old access token, not renewed, answers
401** and its refresh is refused, that a session the change does not name keeps working, and sign in
again (or use `SessionForAsync`) to see the new grants. A change written straight through a service or
`DbContext` revokes nothing: an existing session keeps what it was created with until it is replaced.
Tenancy concepts are in the `multi-tenancy` skill.

## Calling endpoints

Always the type-safe overloads, never raw URLs:

```csharp
var (rsp, res) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
var (rsp, res) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new() { Page = 1, PageSize = 10 });
var (rsp, res) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(request);
var (rsp, res) = await Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(new() { Id = id });
```

For a refusal, use `ProblemDetails` as the response type and assert on the error names (the
camelCased request properties) or the error code:

```csharp
var (rsp, res) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);
rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
res.Errors.Select(e => e.Name).Should().Equal("username", "email", "password", "firstName", "lastName", "roles");

refusal.Errors.First().Code.Should().Be(ErrorCodes.CrossTenantFileAccess.Value);
```

Plan refusals come back as 403 with `ErrorCodes.FeatureDisabled` / `FeatureLimitExceeded`; a missing
permission as 403 `PermissionDenied`. `ErrorCodes` members are `ErrorCode` structs, not strings — read
`.Value` when comparing against a response's `Code` (a plain `string`).

## Shared seed data

`Tests/Seeder` exposes what seeding created — reuse it instead of creating ad-hoc rows:

- `TestUsers`: `PlatformAdminUsername` (`admin`), `TenantAdminUsername` (`tenantadmin`),
  `AdminPassword` (`Admin#123`), `DefaultPassword` (`Test#123`), `PlatformAdminUserId`,
  `TenantAdminUserId`, `TestUserId`, `TestOneUserId`, `TestTwoUserId`, plus `LimitedUserId`
  (one-permission member), `NoMembershipUserId` (no tenant at all) and `DualTenantUserId` (member of
  both seeded tenants).
- `TestRoles`: `AdminRoleId`, `TestRoleId`, `TestOneRoleId`, `TestTwoRoleId`, `LimitedTenantRoleId`,
  `SecondTenantAdminRoleId`, `PlatformAdminRoleId`.
- `TestTenants`: `BootstrapTenantId` (the default tenant), `SecondTenantId`.

Every seeded role except `LimitedTenantRoleId` holds every permission of its scope, so a
permission-denied test uses `LimitedTenantRoleId` or a role made with `CreateTenantRoleAsync`.

## Parallel safety

Test **classes** run in parallel against one database — one collection per class unless the class
says otherwise. This is what keeps the suite fast, so it is worth writing for:

- Never assert on absolute counts or "the first row" of a global list — assert
  `Should().Contain(...)` / `BeGreaterThanOrEqualTo(...)`, or filter to data the test created.
  A delta across two readings is no safer than an absolute count when what is being counted is
  something another class can add to.
- Make fixture data unique: Bogus `f.UniqueIndex` inside a `Faker<T>`, `Guid.NewGuid()` in
  names and keys, `NewTenantIdentifier()` for tenants. `Faker.GlobalUniqueIndex` read directly does
  not make anything unique.
- Never mutate or delete the seeded `admin` / `tenantadmin` users, the seeded roles, or the seeded
  tenants; never write feature values or editions for a seeded tenant — create a tenant.
- Do not depend on ordering between test classes.
- Use `Client` and `Service<T>()`, never the fixture's own client or root provider. `App.Client` is
  a compile error for this reason; for a second identity inside one test use `ClientForAsync` (or
  `App.CreateClient(new ClientOptions { HandleCookies = false })`).
- An override of `SetupAsync`/`TearDownAsync` must call the base implementation, which is what
  releases the test's client and scope.
- If a class genuinely shares a resource with another, give both the same `[Collection]` and say why
  in a comment. The ones that exist are `Notifications` (platform-wide notices reach every account),
  `FileManagement` (one uploads directory), `BootstrapTenant` (tests that modify the default tenant
  row), `FeatureManagement` (feature-value pruning spans the whole table) and `Settings` (a platform
  settings row changes what every tenant resolves to).

## Why the suite is fast

Worth knowing before changing the fixtures, because each of these is load-bearing:

- `Tests/Fakes/TestDoubles.RegisterTestDoubles` substitutes `TestPasswordHasher` for the production
  hasher and `RecordingEmailTransport` for `IEmailTransport` — behind the real `IEmailService`, it
  sends nothing but records each message with the settings it was sent with — and adds the test-only
  probe setting and `PlatformSettingOverlays`. The real hasher is deliberately slow and the suite
  signs in hundreds of times; `PasswordHasherTests` still pins the production parameters.
- No Hangfire worker runs under `Testing`, so enqueued jobs are stored but never executed — assert
  on what the endpoint wrote, not on a job's effect.
- Under `Testing`, `Program.cs` drops the log level to `Warning` and lifts the request rate limit.
  Both defaults are in code rather than in `appsettings.Testing.json`, because that file is not in
  source control.

To run the suite without the `dotnet test` host, execute the built runner directly — it also takes
`-parallel`, `-maxThreads` and `-filter "/*/*/ClassName/*"`:

```sh
./src/backend/Tests/bin/Debug/net10.0/Backend.Tests.exe
```

## Architecture tests

`Tests/Architect` runs with the same suite and fails on:

- cross-feature dependencies (`FeatureDependencyTests`) and direct use of a `[NoDirectUse]` class
  (`NoDirectUseTests`);
- an entity that is neither `IMayHaveTenant` nor `IHaveTenant` and is not in the written exemption
  list (`TenantScopingTests`);
- a permission requiring an undeclared feature, or a `Platform`-scoped permission requiring any
  (`PermissionFeatureDeclarationTests`).

`Tests/Architect/Features/Feature{A,B}` are deliberate violations used as fixtures — leave them
alone.
