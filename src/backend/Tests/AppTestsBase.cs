using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Tenancy;

namespace Backend.Tests;

[Collection("SharedContext")]
/// <summary>
/// Base class for all integration tests providing common setup, authentication, and helper methods.
/// </summary>
public abstract class AppTestsBase(App app) : TestBase<App>
{
    protected readonly App App = app;
    protected AppDbContext DbContext => App.Services.GetRequiredService<AppDbContext>();

    /// <summary>
    /// The tenant scope the current unit of work acts in. Establishing one is what lets a test
    /// arrange tenant-scoped rows directly through <see cref="DbContext"/>, standing in for the
    /// scope a request would have opened.
    /// </summary>
    protected ITenantContext TenantContext => App.Services.GetRequiredService<ITenantContext>();

    /// <summary>
    /// Authenticates the HTTP client by setting a Bearer token obtained from the token endpoint,
    /// optionally selecting a tenant first. Sign-in resolves an active tenant on its own only when
    /// exactly one membership stands, so a tenant is named here whenever the account holds several.
    /// With no arguments the caller is the bootstrap tenant's administrator, acting in that tenant.
    /// </summary>
    protected async Task SetAuthTokenAsync(string username = TestUsers.TenantAdminUsername, string password = TestUsers.AdminPassword, Guid? tenantId = null)
    {
        await TestsHelper.SetNewAuthTokenAsync(App.Client, username, password, tenantId);
    }

    /// <summary>
    /// Authenticates the HTTP client as the seeded platform administrator. The account holds no
    /// membership, so its session acts in no tenant and reaches only what platform administration does.
    /// </summary>
    protected async Task SetPlatformAdminAuthTokenAsync()
    {
        await TestsHelper.SetNewAuthTokenAsync(App.Client, TestUsers.PlatformAdminUsername, TestUsers.AdminPassword);
    }

    /// <summary>
    /// Re-establishes the signed-in caller's session in the tenant named, leaving the client
    /// presenting the token that session issued. The account is unchanged - this is the same caller
    /// acting in another tenant, not a second sign-in.
    /// </summary>
    protected async Task SwitchTenantAsync(Guid tenantId)
    {
        await TestsHelper.SwitchTenantAsync(App.Client, tenantId);
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
        => [.. App.Services
            .GetRequiredService<IPermissionDefinitionService>()
            .GetFlattenedPermissions()
            .Where(permission => permission.Scope == PermissionScope.Platform)
            .Select(permission => permission.Name)];

    /// <summary>
    /// Clears the current authentication token from the HTTP client.
    /// </summary>
    protected void ClearAuthToken()
    {
        App.Client.DefaultRequestHeaders.Authorization = null;
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
        var userService = App.Services.GetRequiredService<IUserService>();
        var roleService = App.Services.GetRequiredService<IRoleService>();
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
