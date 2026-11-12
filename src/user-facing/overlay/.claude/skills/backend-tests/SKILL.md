---
name: backend-tests
description: Write or run xUnit v3 integration tests for the API under src/backend/Tests — App/AppTestsBase fixtures, typed FastEndpoints client calls, Bogus fakers, FluentAssertions, the shared seeder, the suite base classes (accounts, roles, sessions, settings overlays), and the parallel-safety rules. Use whenever an endpoint or backend service is added or changed.
---

# Backend tests

Stack: xUnit v3 + `FastEndpoints.Testing` + FluentAssertions + Bogus. Tests boot the **real** host
(`AppFixture<Program>` with environment `Testing`) against a **real PostgreSQL** matching
`appsettings.Testing.json`; the Testing environment migrates on startup, `App.SetupAsync` runs
`TestsDataSeeder`, and `App.TearDownAsync` drops the database at the end of the run.

`App.PreSetupAsync` first clones the database from a template (`TestDatabaseTemplate`):
`<db>_tpl_<key>` on the same server, a copy of the database as the host leaves it — migrated and
seeded by `DataSeeder`, before `TestsDataSeeder` — keyed on the migrations, `ShareData/` and the
`Seed` configuration and rebuilt at least daily. The first run after any of those change builds the
database the long way and saves a new template; later runs skip the migrations and the seed. Every
failure there is logged and falls back to the long way, so a server without the right to create
databases from a template still runs the suite.

```sh
npm run stop:api    # a running API locks the build output the test run rebuilds
dotnet test --project src/backend/Tests/Backend.Tests.csproj
dotnet test --project src/backend/Tests/Backend.Tests.csproj --filter-class "*UserCreateTests"
dotnet test --project src/backend/Tests/Backend.Tests.csproj --filter-method "*UserCreateTests.Valid_Input"
# several classes in one run, without rebuilding — the cheap way to check a change
dotnet test --project src/backend/Tests/Backend.Tests.csproj --no-build --filter-class "*UserCreateTests" --filter-class "*.Architect.*"
```

Every invocation pays a fixed start-up — host boot, the test database, the seed — of tens of
seconds whatever the filter selects, so one run of three classes costs far less than three runs
of one. Build once, then pass `--no-build` until you edit source again, and combine filters with `|`.

If the run fails at startup, the database is almost always the cause — check that PostgreSQL is up
and the connection string in `appsettings.Testing.json` resolves. Redis is not needed: the Testing
host keeps sessions in one in-memory store.

## Layout

Mirror the source tree: `src/backend/Tests/Features/<Feature>/Endpoints/<Area>/<Entity><Action>Tests.cs`,
and `Features/<Feature>/Core/...` for service tests. One test class per endpoint, named
`<Entity><Action>Tests`; a small slice may group by concern instead (`Features/Seo/SeoPageCrudTests`,
`SeoResolveTests`, `SeoSitemapRobotsTests`, `SeoImageTests`). Cross-cutting suites sit at the root
(`Architect/`, `ErrorHandling/`, `Middleware/`).

## Shape of a test class

```csharp
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Seeder;

namespace Backend.Tests.Features.Identity.Endpoints.Users;

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
  EF Core, `Backend.ErrorHandling`, `Backend.Permissions`, `Backend.Tests.Seeder` and the rest — do
  not re-add them per file.
- Test method names are `Pascal_Snake` describing the case: `Valid_Input`, `Invalid_Input`,
  `List_Users_Pagination`, `Delete_Default_User`.
- Internal endpoint/request types are visible because the API's `Meta.cs` grants
  `InternalsVisibleTo("Backend.Tests")`.
- Pass `TestContext.Current.CancellationToken` to any async EF/service call a test makes directly.

## Base classes

| Base | Adds |
| --- | --- |
| `AppTestsBase` | client, scope, sign-in, setting overlays (table below) |
| `UsersTestsBase` (`Tests/`) | accounts and roles a test owns: `CreateRoleAsync(params permissions)`, `CreateUserAsync(params roleIds)` (password `TestUsers.DefaultPassword`), `SignInAsAsync(username)`, `ClientForAsync(username)` (a second identity on its own client), `SessionForAsync(username)` (a `RenewableSession` that can `RenewAsync()`), `NewAccountIdentity()`, `UserService` |
| `SessionRevocationTestsBase` (`Tests/Features/Identity`) | `AssertEndedAsync` / `AssertAliveAsync` over a `SessionForAsync` session, `RefreshStatusAsync`, `InfoStatusAsync`, `SessionIdOf`, `PermissionIdAsync` |
| `SettingsTestsBase` (`Tests/Features/Settings`) | the `Settings` collection: `SettingsClientAsync(params permissions)`, `AdminClientAsync()`, `StoredValuesAsync`, `SetStoredValuesAsync`, `PropertyOf`, and a teardown that removes every stored setting row (see the `settings` skill) |
| `SeoTestsBase` (`Tests/Features/Seo`) | on top of `SettingsTestsBase`: `NewPath()`, `AnonymousClient()`, `CreatePageAsync`, `ResolveAsync`, `SitemapAsync`, `SetSeoAsync`, and a teardown that drops `ISeoCache` |
| `NotificationsTestsBase`, `FileTestsBase`, `LocalizationTestsBase` | the arrangements those suites repeat, and their `[Collection]` |

Anything that needs its own role or account should derive from `UsersTestsBase` and create it
rather than reuse a seeded one. If several tests in a new area need the same setup, add an
abstract `<Area>TestsBase` on top of the closest of these.

## What `AppTestsBase` gives you

| Member | Use |
| --- | --- |
| `Client` | this test's own typed HTTP client — its bearer token is seen by no other test |
| `Service<T>()` | resolve a service in this test's scope (`Service<IRoleService>()`) |
| `DbContext` | direct EF access for arranging/asserting state, in the same scope |
| `SetAuthTokenAsync(username, password)` | sets the Bearer header; defaults to the seeded administrator `admin` with `TestUsers.AdminPassword` |
| `ClearAuthToken()` | test the unauthenticated path |
| `SessionOfAsync(accessToken)` | the `SessionRecord` a token names |
| `OverlaySetting(client, settingName, values)` | that client's requests see `values` as the stored overrides of one setting; no other request does |
| `AllowSignup(client)` / `SignupVisitorClient()` | a client whose requests see `General.IsSignupEnabled` on |
| `DeleteAfterTest(uploaded)` | removes an uploaded file's content when the test tears down |
| `CreateAdminUserAsync(username, password)` | a fresh account in the `Admin` role (throws if it already exists) |

A token holds only the account and `sid`; roles and permissions live in the session the store
holds, which every request reads. A change made through an endpoint that revokes — a user
deactivated, deleted or re-roled, a password changed or reset, a role's grants replaced or the role
deleted — ends the affected sessions at once, so assert that the **old access token, not renewed,
answers 401** and its refresh is refused (`AssertEndedAsync`), that a session the change does not
name keeps working (`AssertAliveAsync`), and sign in again (or use `SessionForAsync`) to see the
new grants. A change written straight through a service or `DbContext` revokes nothing: an existing
session keeps what it was created with until it is replaced.

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

refusal.Errors.First().Code.Should().Be(ErrorCodes.SeoPageAlreadyExists.Value);
```

A missing permission comes back as 403 `PermissionDenied`, no session as 401. `ErrorCodes` members
are `ErrorCode` structs, not strings — read `.Value` when comparing against a response's `Code` (a
plain `string`).

**Anonymous endpoints** are tested from a client with no token (`AnonymousClient()`, or
`App.CreateClient(new ClientOptions { HandleCookies = false })`): assert what they return, and that
they return nothing beyond what is public — `SeoImageTests` proves an existing file no page names
answers 404.

## Shared seed data

`Tests/Seeder` exposes what seeding created — reuse it instead of creating ad-hoc rows:

- `TestUsers`: `AdminUsername` (`admin`), `AdminPassword` (the `Seed:AdminPassword` configuration
  value, read when the fixture seeds — never hardcode it), `DefaultPassword` (`Test#123`),
  `AdminUserId`, `TestUserId`, `TestOneUserId`, `TestTwoUserId`, `LimitedUserId` (one permission,
  `Seo.View`) and `PlainUserId` (in the `User` role sign-up assigns, which holds no permission).
- `TestRoles`: `AdminRoleId`, `UserRoleId`, `TestRoleId`, `TestOneRoleId`, `TestTwoRoleId`,
  `LimitedRoleId`.

The `Admin`, `Test`, `TestOne` and `TestTwo` roles hold every permission, so a permission-denied
test uses `LimitedRoleId`, the `plain` account, or a role made with `CreateRoleAsync`.

## Parallel safety

Test **classes** run in parallel against one database — one collection per class unless the class
says otherwise. This is what keeps the suite fast, so it is worth writing for:

- Never assert on absolute counts or "the first row" of a global list — assert
  `Should().Contain(...)` / `BeGreaterThanOrEqualTo(...)`, or filter to data the test created.
  A delta across two readings is no safer than an absolute count when what is being counted is
  something another class can add to.
- Make fixture data unique: Bogus `f.UniqueIndex` inside a `Faker<T>`, `Guid.NewGuid()` in
  names, keys and paths (`NewPath()`, `NewAccountIdentity()`). `Faker.GlobalUniqueIndex` read
  directly does not make anything unique.
- Never mutate or delete the seeded `admin` account, the seeded roles or the seeded SEO pages —
  create your own.
- **Never store a setting row another class's requests would read.** A stored `General` row is read
  by every sign-in (`IsEmailVerificationRequired` on would refuse every unverified test account);
  show what an override does with `OverlaySetting` on one client instead. Rows only the settings
  suite reads are written inside the `Settings` collection, whose base removes them after each test.
- Do not depend on ordering between test classes.
- Use `Client` and `Service<T>()`, never the fixture's own client or root provider. `App.Client` is
  a compile error for this reason; for a second identity inside one test use `ClientForAsync` (or
  `App.CreateClient(new ClientOptions { HandleCookies = false })`).
- An override of `SetupAsync`/`TearDownAsync` must call the base implementation, which is what
  releases the test's client and scope.
- If a class genuinely shares a resource with another, give both the same `[Collection]` and say why
  in a comment. The ones that exist are `Notifications` (all-users broadcasts reach every account),
  `FileManagement` (one uploads directory), `Localization` (a text or language row changes what
  every request resolves to) and `Settings` (a stored setting row changes what every request
  resolves to; the SEO suite joins it because its endpoints read the `Seo` setting through one
  process-wide cache).
- A test that uploads a file deletes it: pass the `FileUploadResponse` to `DeleteAfterTest(uploaded)`
  straight after the `POSTAsync` and before any assertion, so a failing test cleans up too, and the
  file's content is removed when the test tears down (`FileTestsBase.UploadAsync` already does this).
  Uploads land in `<ContentRoot>/uploads` under `Source/`, which nothing else clears. As a safety net,
  `RecordingStorageProvider` records every file the host saves and `App` removes whatever is left when
  the run ends, but a missed `DeleteAfterTest` is still a bug.

## Why the suite is fast

Worth knowing before changing the fixtures, because each of these is load-bearing:

- `Tests/Fakes/TestDoubles.RegisterTestDoubles` substitutes `TestPasswordHasher` for the production
  hasher and `RecordingEmailTransport` for `IEmailTransport` — behind the real `IEmailService`, it
  sends nothing but records each message with the settings it was sent with — wraps the session
  store in `FaultInjectingSessionStore` (a test can make the store unreachable for sessions or
  accounts it created itself) beside a `FaultingSessionEndedHandler`, and adds the test-only probe
  setting (`ProbeSettings`), `SettingOverlays` and `RecordingStorageProvider` around the storage
  provider. The real hasher is deliberately slow and the suite signs in hundreds of times;
  `PasswordHasherTests` still pins the production parameters.
- No Hangfire worker runs under `Testing`, so enqueued jobs are stored but never executed — assert
  on what the endpoint wrote, not on a job's effect.
- Under `Testing`, `Program.cs` drops the log level to `Warning` and lifts the request rate limit.
  Both defaults are in code rather than in `appsettings.Testing.json`, because that file is not in
  source control.

`dotnet test` runs on Microsoft.Testing.Platform (opted in by `test.runner` in `global.json`, required by xunit.v3 4.x): the project goes after `--project`, and filters are `--filter-class`, `--filter-method` and `--filter-namespace` (wildcards allowed; repeating one is OR, mixing kinds is AND), not `--filter`. To run the suite without the `dotnet test` host, execute the built runner directly — it also takes
`-parallelMode`, `-maxThreads` and `-class "<fully qualified name>"`:

```sh
./src/backend/Tests/bin/Debug/net10.0/Backend.Tests.exe
```

## Architecture tests

`Tests/Architect` runs with the same suite and fails on cross-feature dependencies
(`FeatureDependencyTests`) and direct use of a `[NoDirectUse]` class (`NoDirectUseTests`).

`Tests/Architect/Features/Feature{A,B}` are deliberate violations used as fixtures — leave them
alone.
