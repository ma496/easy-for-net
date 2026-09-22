namespace Backend.Tests;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Tenancy;

/// <summary>
/// Base class for all integration tests providing common setup, authentication, and helper methods.
/// </summary>
/// <remarks>
/// It derives from the plain <c>TestBase</c> rather than <c>TestBase&lt;App&gt;</c> because
/// <see cref="App"/> is an assembly fixture, not a class fixture - it is built once for the whole
/// run and handed to every test class's constructor. It carries no collection attribute either, so
/// xunit puts each test class in a collection of its own and runs them concurrently; a class that
/// has to be serialised against another names a collection for itself.
/// </remarks>
public abstract class AppTestsBase(App app) : TestBase
{
    protected readonly App App = app;

    private AsyncServiceScope _scope;
    private bool _scopeCreated;
    private HttpClient? _client;

    /// <summary>
    /// The service scope this test acts in - one unit of work, like the one a request would open.
    /// </summary>
    /// <remarks>
    /// It is created on first use rather than in setup so that it cannot be missed by a derived class
    /// that overrides <c>SetupAsync</c> without calling this one. Resolving these services from the
    /// host's root provider instead would hand every test the same <see cref="AppDbContext"/> and the
    /// same <see cref="ITenantContext"/> - one change tracker for the whole run, and one mutable
    /// tenant scope that concurrent tests would take from each other.
    /// </remarks>
    private IServiceProvider Scoped
    {
        get
        {
            if (!_scopeCreated)
            {
                _scope = App.Services.CreateAsyncScope();
                _scopeCreated = true;
            }

            return _scope.ServiceProvider;
        }
    }

    /// <summary>
    /// Resolves a service in this test's scope.
    /// </summary>
    protected T Service<T>() where T : notnull => Scoped.GetRequiredService<T>();

    /// <summary>
    /// This test's own HTTP client. The bearer token set on it belongs to this test and is seen by
    /// no other, which is what lets test classes run at the same time.
    /// </summary>
    protected HttpClient Client => _client ??= App.CreateClient(new ClientOptions());

    protected AppDbContext DbContext => Service<AppDbContext>();

    /// <summary>
    /// The tenant scope the current unit of work acts in. Establishing one is what lets a test
    /// arrange tenant-scoped rows directly through <see cref="DbContext"/>, standing in for the
    /// scope a request would have opened.
    /// </summary>
    protected ITenantContext TenantContext => Service<ITenantContext>();

    /// <summary>
    /// Releases this test's client and scope. An override in a derived class must call this one.
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
        _client?.Dispose();

        if (_scopeCreated)
        {
            await _scope.DisposeAsync();
        }

        await base.TearDownAsync();
    }

    /// <summary>
    /// Authenticates the HTTP client by setting a Bearer token obtained from the token endpoint,
    /// naming the tenant to sign in to when one is given. Sign-in resolves a tenant by itself only
    /// when exactly one active membership stands, and refuses an ordinary account holding none or
    /// several, so a tenant is named here whenever the account holds anything but one. With no
    /// arguments the caller is the bootstrap tenant's administrator, acting in that tenant.
    /// </summary>
    protected async Task SetAuthTokenAsync(string username = TestUsers.TenantAdminUsername, string password = TestUsers.AdminPassword, Guid? tenantId = null)
    {
        await TestsHelper.SetNewAuthTokenAsync(Client, username, password, await TenantIdentifierOfAsync(tenantId));
    }

    /// <summary>
    /// The url-safe identifier of a tenant named by its key, which is what sign-in takes. Tests hold
    /// the tenants they create by identity, so the translation happens here once rather than at every
    /// call site.
    /// </summary>
    /// <param name="tenantId">The tenant to name, or <see langword="null"/> to name none.</param>
    /// <returns>The tenant's identifier, or <see langword="null"/> when no tenant was named.</returns>
    protected async Task<string?> TenantIdentifierOfAsync(Guid? tenantId)
        => tenantId is { } id
            ? await DbContext.Tenants
                .AsNoTracking()
                .AcrossAllTenants()
                .Where(tenant => tenant.Id == id)
                .Select(tenant => tenant.Identifier)
                .SingleAsync(TestContext.Current.CancellationToken)
            : null;

    /// <summary>
    /// Authenticates the HTTP client as the seeded platform administrator. The account holds no
    /// membership, so its session acts in no tenant and reaches only what platform administration does.
    /// </summary>
    protected async Task SetPlatformAdminAuthTokenAsync()
    {
        await TestsHelper.SetNewAuthTokenAsync(Client, TestUsers.PlatformAdminUsername, TestUsers.AdminPassword);
    }

    /// <summary>
    /// Re-establishes the signed-in caller's session in the tenant named, leaving the client
    /// presenting the token that session issued. The account is unchanged - this is the same caller
    /// acting in another tenant, not a second sign-in.
    /// </summary>
    protected async Task SwitchTenantAsync(Guid tenantId)
    {
        await TestsHelper.SwitchTenantAsync(Client, tenantId);
    }

    /// <summary>
    /// Marks an account as belonging to the platform tier, which is what admits it to platform scope
    /// and lets it enter a tenant it holds no membership of. The tier is a column on the account
    /// rather than anything a role grants, so a test that wants a platform caller sets it here as well
    /// as assigning the platform-scoped role that carries the permissions.
    /// </summary>
    /// <param name="userId">The account to mark.</param>
    protected async Task MarkAsPlatformAccountAsync(Guid userId)
    {
        var account = await DbContext.Users.SingleAsync(candidate => candidate.Id == userId, TestContext.Current.CancellationToken);
        account.IsPlatform = true;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The names of the permissions the code-declared catalogue makes exercisable only in platform
    /// scope - the ones a tenant role may never hold and a session acting inside a tenant never
    /// carries. Permissions declared for both scopes are deliberately left out: they are exercisable
    /// in a tenant too, so finding one there proves nothing went wrong.
    /// </summary>
    protected IReadOnlyCollection<string> PlatformOnlyPermissionNames()
        => [.. Service<IPermissionDefinitionService>()
            .GetFlattenedPermissions()
            .Where(permission => permission.Scope == PermissionScope.Platform)
            .Select(permission => permission.Name)];

    /// <summary>
    /// Clears the current authentication token from the HTTP client.
    /// </summary>
    protected void ClearAuthToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Creates a new admin user with the specified credentials, assigning the Admin role.
    /// </summary>
    /// <remarks>
    /// The account is created inside the bootstrap tenant's scope, so the role it is granted is that
    /// tenant's administrator role - the one a caller acting there actually holds - and it joins the
    /// bootstrap tenant as its only membership, which keeps sign-in resolving an active tenant for it
    /// exactly as it does for every other seeded account.
    /// </remarks>
    /// <exception cref="Exception">Thrown when the admin role does not exist or the user already exists.</exception>
    protected async Task<User> CreateAdminUserAsync(string username, string password)
    {
        var userService = Service<IUserService>();
        var roleService = Service<IRoleService>();
        var user = await userService.GetByUsernameAsync(username);
        if (user == null)
        {
            using var bootstrapTenant = TenantContext.BeginTenant(TestTenants.BootstrapTenantId);

            user = await userService.CreateAsync(new User
            {
                SystemCreated = true,
                Username = username,
                Email = $"{username}@example.com"
            }, password);
            user.NormalizeProperties();
            // Assign admin role to the user
            var adminRole = await roleService.GetByNameAsync("Admin");
            if (adminRole == null)
            {
                throw new Exception("Admin role does not exist. Please ensure it is created before running the tests.");
            }
            await userService.AssignRoleAsync(user.Id, adminRole.Id);
            return user;
        }
        else
            throw new Exception($"Admin user ({username}) already exists. Please choose a different username.");
    }
}
