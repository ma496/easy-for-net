namespace Backend.Tests.Features.Tenancy;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Tenancy;

/// <summary>
/// Base class for the tenancy suite, in the shape of <c>NotificationsTestsBase</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every tenant, role, membership and account a tenancy test asserts on is made by the test itself
/// through the factories below. That is the point rather than a style: the suite runs its collections
/// in parallel against one shared database, so a test that asserted on rows it did not create - a
/// count of every tenant, "the first row" of a list, a seeded account's memberships - would be racing
/// every other test that touches the same rows.
/// </para>
/// <para>
/// <see cref="TestTenants.BootstrapTenantId"/> and <see cref="TestTenants.SecondTenantId"/> are for
/// what only a seeded tenant can prove - that the bootstrap tenant is system-created, that a seeded
/// account is where the seeder put it - and are read, never written. Nothing here suspends, deletes,
/// renames or joins a seeded tenant, and nothing adds a membership to a seeded account: the seeder's
/// one-membership invariant is what keeps sign-in resolving an active tenant for every existing test.
/// </para>
/// </remarks>
public abstract class TenancyTestsBase(App app) : AppTestsBase(app)
{
    protected ITenantService TenantService => App.Services.GetRequiredService<ITenantService>();

    protected ITenantAuthorizationService TenantAuthorizationService => App.Services.GetRequiredService<ITenantAuthorizationService>();

    /// <summary>
    /// The membership service, for tests that arrange or observe a membership directly rather than
    /// through the surface that administers it - the membership path the endpoints themselves take.
    /// </summary>
    protected ITenantMembershipService MembershipService => App.Services.GetRequiredService<ITenantMembershipService>();

    protected IUserService UserService => App.Services.GetRequiredService<IUserService>();

    /// <summary>
    /// Creates a tenant with a unique identifier valid under AC-101, under platform scope - the
    /// standing a tenant is created from, since a tenant belongs to no tenant of its own.
    /// </summary>
    /// <param name="status">The state to leave the tenant in. Suspension is applied after creation because a tenant is always born active.</param>
    /// <returns>The created tenant, carrying its assigned identity.</returns>
    protected async Task<Tenant> CreateTenantAsync(TenantStatus status = TenantStatus.Active)
    {
        using var platformScope = TenantContext.BeginPlatformScope();

        var tenant = await TenantService.CreateAsync(new Tenant
        {
            Name = $"Tenant {Faker.GlobalUniqueIndex}",
            Identifier = NewTenantIdentifier()
        });

        if (status != TenantStatus.Active)
        {
            tenant.Status = status;
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return tenant;
    }

    /// <summary>
    /// Creates a role inside a tenant holding exactly the permissions named - and no others, which
    /// is what lets a test prove a per-endpoint permission declaration rather than a caller's
    /// blanket authority.
    /// </summary>
    /// <param name="tenantId">The tenant the role belongs to.</param>
    /// <param name="permissions">The permission names the role is to hold.</param>
    /// <returns>The identifier of the created role.</returns>
    protected async Task<Guid> CreateTenantRoleAsync(Guid tenantId, params string[] permissions)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        var role = new Role
        {
            SystemCreated = false,
            // A role name is unique within its tenant, so it is derived from a fresh identifier rather
            // than a counter: a test may give one tenant several roles, and a name that repeated would
            // be refused by the database rather than naming a second role.
            Name = $"Role {Guid.NewGuid():N}",
            Description = "Role made by a tenancy test"
        };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var permissionIds = await DbContext.Permissions
            .Where(permission => permissions.Contains(permission.Name))
            .Select(permission => permission.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        DbContext.RolePermissions.AddRange(permissionIds.Select(permissionId => new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permissionId
        }));
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    /// <summary>
    /// Creates an account with the shared test password, an active membership of the tenant named and
    /// exactly the roles named. The membership is written by account creation itself - an account
    /// created while acting inside a tenant joins it - so it holds exactly one, and sign-in keeps
    /// resolving that tenant for it.
    /// </summary>
    /// <param name="tenantId">The tenant the account is to be a member of.</param>
    /// <param name="roleIds">The roles the account holds in that tenant, and no others.</param>
    /// <returns>The created account.</returns>
    protected async Task<User> CreateTenantUserAsync(Guid tenantId, params Guid[] roleIds)
    {
        var user = await CreateAccountAsync(tenantId);

        if (roleIds.Length > 0)
        {
            await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenantId, user.Id, roleIds);
        }

        return user;
    }

    /// <summary>
    /// Creates an active account holding no membership at all - the AC-050/AC-122 standing, in which
    /// every tenant-scoped call is refused with an explanation rather than answered from nothing.
    /// </summary>
    /// <returns>The created account.</returns>
    protected async Task<User> CreateAccountWithoutMembershipAsync()
        => await CreateAccountAsync(tenantId: null);

    /// <summary>
    /// Creates an account that is a member of both tenants named and holds no role in either beyond what
    /// joining grants it - one identity acting in two tenants, which is the standing every claim about
    /// what a caller sees in one tenant and not in another is stated over.
    /// </summary>
    /// <remarks>
    /// The account is made for this purpose rather than taken from the seed: the memberships are what let
    /// one identity act in either tenant, and adding a second one to a seeded account would change the
    /// tenants every other test signs in to.
    /// </remarks>
    /// <param name="firstTenantId">The first tenant the account is to be a member of.</param>
    /// <param name="secondTenantId">The second tenant the account is to be a member of.</param>
    /// <returns>The created account.</returns>
    protected async Task<User> CreateDualTenantMemberAsync(Guid firstTenantId, Guid secondTenantId)
    {
        var account = await CreateAccountWithoutMembershipAsync();

        await MembershipService.AddAsync(firstTenantId, account.Id, [], TestContext.Current.CancellationToken);
        await MembershipService.AddAsync(secondTenantId, account.Id, [], TestContext.Current.CancellationToken);

        return account;
    }

    /// <summary>
    /// Creates a platform administrator that acts inside a tenant of its own, and signs the fixture's
    /// client in as it. Acting in a tenant is what widens the user and role endpoints across every
    /// tenant for a platform administrator; the seeded one acts in no tenant, where those endpoints
    /// answer about the platform's own users and roles instead.
    /// </summary>
    /// <returns>The created account.</returns>
    protected async Task<User> SignInAsPlatformAdministratorActingInATenantAsync()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);

        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);
        await SignInAsAsync(account.Username);

        return account;
    }

    /// <summary>
    /// Signs the fixture's client in as an account, optionally selecting a tenant first, so that
    /// later requests on it are made by that caller. Leaves the client authenticated as that account.
    /// </summary>
    /// <param name="username">The account to sign in as.</param>
    /// <param name="tenantId">The tenant to act in, or <see langword="null"/> to leave the account in whatever state sign-in resolved.</param>
    protected async Task SignInAsAsync(string username, Guid? tenantId = null)
        => await SetAuthTokenAsync(username, TestUsers.DefaultPassword, tenantId);

    /// <summary>
    /// Returns a second HTTP client signed in as an account, for a test that needs two identities at
    /// once. The fixture's own client carries one bearer token for the whole test, so the second
    /// identity has to have a client of its own rather than sharing that one.
    /// </summary>
    /// <param name="username">The account the new client is to act as.</param>
    /// <param name="tenantId">The tenant that client is to act in, or <see langword="null"/> to leave it in whatever state sign-in resolved.</param>
    /// <returns>A client presenting that account's bearer token and nothing else.</returns>
    protected async Task<HttpClient> ClientForAsync(string username, Guid? tenantId = null)
    {
        // Cookies are not carried, so the client's identity is exactly the bearer token set here and
        // can never drift to whoever last re-established a session through this handler.
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(client, username, TestUsers.DefaultPassword, tenantId);
        return client;
    }

    /// <summary>
    /// Runs a block with a tenant scope established, for a test that arranges tenant-scoped rows
    /// through <see cref="AppTestsBase.DbContext"/> directly - standing in for the scope a request
    /// would have opened, which is what save-time attribution would otherwise refuse the write for.
    /// </summary>
    /// <param name="tenantId">The tenant the block acts in.</param>
    /// <param name="action">The arrangement to run.</param>
    protected async Task TenantScopedAsync(Guid tenantId, Func<Task> action)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);
        await action();
    }

    /// <summary>
    /// An identifier no other test can collide with, in the shape AC-101 accepts: lower-case letters,
    /// digits and single hyphens, beginning and ending with a letter or a digit.
    /// </summary>
    protected static string NewTenantIdentifier() => $"t-{Guid.NewGuid():N}";

    /// <summary>
    /// A username and the email built from it that no run of the suite can collide with. Account names
    /// are globally unique - unlike a tenant identifier or a role name, which are only compared within
    /// their own scope - and the test database is neither wiped nor recreated between runs, so a name
    /// derived from a counter that restarts with the process would collide with the rows the previous
    /// run left behind.
    /// </summary>
    /// <returns>The username, and the email address belonging to it.</returns>
    private static (string Username, string Email) NewAccountIdentity()
    {
        var username = $"u{Guid.NewGuid():N}";
        return (username, $"{username}@example.com");
    }

    /// <summary>
    /// Creates an account with the shared test password, joined to the tenant named when one is, and
    /// to no tenant at all when none is - the platform standing, in which account creation writes no
    /// membership, which is what makes an account with none possible.
    /// </summary>
    private async Task<User> CreateAccountAsync(Guid? tenantId)
    {
        using var scope = tenantId is { } activeTenantId
            ? TenantContext.BeginTenant(activeTenantId)
            : TenantContext.BeginPlatformScope();

        var (username, email) = NewAccountIdentity();
        return await UserService.CreateAsync(new User
        {
            SystemCreated = false,
            Username = username,
            Email = email
        }, TestUsers.DefaultPassword);
    }
}
