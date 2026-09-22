namespace Backend.Tests.Seeder;

using Backend.ShareData.Entities;
using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Seeds test data into the database including tenants, users, roles, memberships and permissions.
/// Populates the shared <see cref="TestUsers"/>, <see cref="TestRoles"/> and <see cref="TestTenants"/>
/// static holders with generated IDs.
/// </summary>
/// <remarks>
/// <para>
/// The one invariant everything else here is arranged around: <b>every seeded account except
/// <c>dual</c> holds exactly one active membership</b>. Sign-in auto-selects the active tenant only
/// when exactly one stands (<c>TokenEndpoint.ResolveSingleActiveTenantAsync</c>), so one membership
/// each is what keeps <c>SetAuthTokenAsync()</c> working unchanged for the existing test methods
/// across <c>Users</c>, <c>Roles</c>, <c>Account</c> and <c>Notifications</c>. Giving <c>tenantadmin</c>,
/// <c>test</c>, <c>testone</c> or <c>testtwo</c> a second membership breaks the whole existing suite.
/// The platform administrator <c>admin</c> holds no membership at all, and must keep holding none.
/// </para>
/// <para>
/// <c>dual</c> is the deliberate exception and the only account that exercises the "no active tenant
/// at sign-in" path (AC-140/AC-149). <c>nomember</c> is the other way into that state - an account
/// with no membership at all (AC-050/AC-122) - and differs from <c>dual</c> in that no tenant is
/// available to choose either.
/// </para>
/// </remarks>
public class TestsDataSeeder(IUserService userService,
                             IRoleService roleService,
                             IPermissionService permissionService,
                             ITenantService tenantService,
                             ITenantAuthorizationService tenantAuthorizationService,
                             ITenantContext tenantContext,
                             AppDbContext dbContext)
{
    /// <summary>
    /// Seeds the bootstrap tenant's members, a second tenant with its own administrator, the
    /// single-permission role and the accounts the tenancy suite needs, capturing every generated ID
    /// on the static holders. Idempotent: the shared fixture seeds once, but the seeder is written so
    /// that a second pass finds what the first one made rather than duplicating it.
    /// </summary>
    public async Task SeedAsync()
    {
        var permissions = await permissionService.Permissions().ToListAsync();

        var (testUserId, testRoleId) = await CreateUserWithRoleAsync(permissions, "test", "Test");
        var (testOneUserId, testOneRoleId) = await CreateUserWithRoleAsync(permissions, "testone", "TestOne");
        var (testTwoUserId, testTwoRoleId) = await CreateUserWithRoleAsync(permissions, "testtwo", "TestTwo");

        var limitedTenantRoleId = await CreateLimitedTenantRoleAsync(permissions);

        // The bootstrap tenant's administrator role is the one the platform's seeder provisioned
        // inside that tenant, and every seeded account acts there, so it is read inside the
        // bootstrap scope: read from outside, the same lookup would find the platform's own "Admin"
        // role instead, which belongs to no tenant and is held by no seeded account.
        var adminRoleId = await ReadBootstrapAdministratorRoleIdAsync();

        // Created before the tenants that will hold them, and outside every tenant scope, so that
        // UserService.CreateAsync writes no membership for them: which tenants they end up in is
        // decided below, one membership at a time, rather than by the act of creating the account.
        var limitedUserId = await CreateAccountAsync("limited");
        var noMembershipUserId = await CreateAccountAsync("nomember");
        var dualTenantUserId = await CreateAccountAsync("dual");
        var secondTenantAdminUserId = await CreateAccountAsync("secondadmin");

        // limited: the bootstrap tenant, holding nothing but the single-permission role. This is the
        // account AC-088's gate test signs in as, so it must hold no other permission anywhere.
        await JoinTenantAsync(TestTenants.BootstrapTenantId, limitedUserId, [limitedTenantRoleId]);

        // dual's first membership, and the other half of the state it exists for: one account in two
        // tenants, so signing in can resolve neither of them and has to ask. Both are granted before
        // the second tenant is created below, because a membership is written one tenant at a time and
        // neither of these depends on the other.
        await JoinTenantAsync(TestTenants.BootstrapTenantId, dualTenantUserId, [limitedTenantRoleId]);

        // The second tenant, provisioned through the one creation path every tenant goes through, so
        // its administrator role exists for the same reason a real tenant's does. `secondadmin` is
        // named as the first member, which is what makes it that tenant's administrator.
        var secondTenant = await tenantService.CreateAsync(
            new Tenant { Name = "Second Tenant", Identifier = "second-tenant" },
            secondTenantAdminUserId);

        var secondTenantAdminRoleId = await ReadAdministratorRoleIdAsync(secondTenant.Id);

        // dual: a member of both seeded tenants, which is exactly what puts it in the state this
        // suite is otherwise missing - signed in, able to act in either, and acting in neither until
        // it chooses. Administrator in the second tenant, single-permission role in the bootstrap
        // one, so a switch between the two also proves authority does not travel with the account.
        await JoinTenantAsync(secondTenant.Id, dualTenantUserId, [secondTenantAdminRoleId]);

        await SeedStoredFilesForProfileImagesAsync();

        TestUsers.SetUserIds(
            platformAdminUserId: await ReadUserIdAsync(TestUsers.PlatformAdminUsername),
            tenantAdminUserId: await ReadUserIdAsync(TestUsers.TenantAdminUsername),
            testUserId,
            testOneUserId,
            testTwoUserId);
        TestUsers.SetTenantUserIds(limitedUserId, noMembershipUserId, dualTenantUserId);
        TestRoles.SetRoleIds(adminRoleId, testRoleId, testOneRoleId, testTwoRoleId);
        TestRoles.SetTenantRoleIds(limitedTenantRoleId, secondTenantAdminRoleId, await ReadPlatformAdministratorRoleIdAsync());
        TestTenants.SetTenantIds(TenancyConstants.BootstrapTenantId, secondTenant.Id);
    }

    /// <summary>
    /// Creates a role inside the bootstrap tenant holding exactly one permission, so that a
    /// per-endpoint <c>Permissions(...)</c> declaration can be proved to be what turns a caller away:
    /// every other seeded role holds every permission, which would make the refusal unprovable. The
    /// permission has to be one a tenant role can actually exercise, or every endpoint would refuse the
    /// holder and the gate would be unprovable for the opposite reason.
    /// </summary>
    private async Task<Guid> CreateLimitedTenantRoleAsync(List<Permission> permissions)
    {
        using (tenantContext.BeginTenant(TestTenants.BootstrapTenantId))
        {
            var role = await roleService.GetByNameAsync("LimitedTenant")
                       ?? await roleService.CreateAsync(new Role
                       {
                           SystemCreated = true,
                           Name = "LimitedTenant",
                           Description = "Limited Tenant Role"
                       });

            var grantedPermissionNames = await roleService.GetRolePermissionsAsync(role.Id);
            if (!grantedPermissionNames.Contains(Allow.Tenant_Detail))
            {
                var tenantDetailPermission = permissions.Single(permission => permission.Name == Allow.Tenant_Detail);
                await roleService.AssignPermissionAsync(role.Id, tenantDetailPermission.Id);
            }

            return role.Id;
        }
    }

    /// <summary>
    /// Creates a user with a specific role, assigning all available permissions to that role, and -
    /// because the whole of this runs inside the bootstrap tenant's scope - an active membership of
    /// the bootstrap tenant. One membership each is what keeps sign-in resolving an active tenant for
    /// the existing tests.
    /// </summary>
    private async Task<(Guid userId, Guid roleId)> CreateUserWithRoleAsync(List<Permission> permissions, string username, string roleName)
    {
        using (tenantContext.BeginTenant(TestTenants.BootstrapTenantId))
        {
            var role = await roleService.GetByNameAsync(roleName) ??
                await roleService.CreateAsync(new Role { SystemCreated = true, Name = roleName });
            var rolePermissions = await permissionService.GetRolePermissionsAsync(role.Id);
            var permissionsToAssign = permissions.Where(p => !rolePermissions.Any(rp => rp.Name == p.Name)).ToList();
            foreach (var permission in permissionsToAssign)
            {
                await roleService.AssignPermissionAsync(role.Id, permission.Id);
            }

            var user = await userService.GetByUsernameAsync(username) ??
                await userService.CreateAsync(new User { SystemCreated = true, Username = username, Email = $"{username}@example.com" }, TestUsers.DefaultPassword);
            if (!await userService.IsInRoleAsync(user.Id, role.Id))
            {
                await userService.AssignRoleAsync(user.Id, role.Id);
            }
            return (user.Id, role.Id);
        }
    }

    /// <summary>
    /// Creates an active account and deliberately joins it to no tenant, so that the memberships it
    /// is to hold are named one at a time by the caller rather than implied by a scope.
    /// </summary>
    private async Task<Guid> CreateAccountAsync(string username)
    {
        // Platform scope rather than no scope at all: it reads as "acting in no tenant", which is
        // precisely the state in which creating an account writes no membership, and it says so
        // explicitly instead of relying on the seeder never having established anything.
        using (tenantContext.BeginPlatformScope())
        {
            var user = await userService.GetByUsernameAsync(username) ??
                await userService.CreateAsync(new User { SystemCreated = true, Username = username, Email = $"{username}@example.com" }, TestUsers.DefaultPassword);
            return user.Id;
        }
    }

    /// <summary>
    /// Gives an account an active membership of a tenant and replaces its roles inside that tenant
    /// with exactly the ones named. The replacement is scoped to this tenant's roles, so it leaves
    /// the account's standing in every other tenant untouched.
    /// </summary>
    private async Task JoinTenantAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds)
    {
        var alreadyAMember = await dbContext.TenantMemberships
            .AcrossAllTenants()
            .AnyAsync(membership => membership.TenantId == tenantId && membership.UserId == userId);

        if (!alreadyAMember)
        {
            using (tenantContext.BeginTenant(tenantId))
            {
                dbContext.TenantMemberships.Add(new TenantMembership { UserId = userId });
                await dbContext.SaveChangesAsync();
            }
        }

        await tenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(tenantId, userId, roleIds);
    }

    /// <summary>
    /// Writes the attribution record behind every seeded profile image, so that an authenticated read
    /// of seeded data resolves to a file rather than to a missing one. The record is account-owned:
    /// a profile image belongs to the account, not to whatever tenant its owner happened to be acting
    /// in when it was set, so its owner reads it while acting in any tenant, or in none.
    /// </summary>
    /// <remarks>
    /// The tenancy migration backfills these rows from the accounts that already carry an image, but
    /// the seeder creates its accounts afterwards, so it maintains the same invariant for the ones it
    /// makes. Rows whose content was never written to storage would resolve as a missing file anyway,
    /// so this only ever adds the record for an image that has a name.
    /// </remarks>
    private async Task SeedStoredFilesForProfileImagesAsync()
    {
        var images = await dbContext.Users
            .Where(account => account.Image != null && account.Image != "")
            .Select(account => new { account.Id, Image = account.Image! })
            .ToListAsync();

        if (images.Count == 0)
        {
            return;
        }

        var recordedFileNames = await dbContext.StoredFiles
            .AcrossAllTenants()
            .Select(storedFile => storedFile.FileName)
            .ToListAsync();

        var missing = images.Where(image => !recordedFileNames.Contains(image.Image)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        // Platform scope, because an account-owned file is attributed to no tenant and the save would
        // otherwise stamp the active tenant onto it and quietly make it that tenant's data.
        using (tenantContext.BeginPlatformScope())
        {
            dbContext.StoredFiles.AddRange(missing.Select(image => new StoredFile
            {
                TenantId = null,
                OwnerUserId = image.Id,
                FileName = image.Image,
                OriginalFileName = image.Image,
                ContentType = "application/octet-stream"
            }));
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Reads the bootstrap tenant's administrator role, which is the role a seeded account acting in
    /// that tenant is actually granted.
    /// </summary>
    private async Task<Guid> ReadBootstrapAdministratorRoleIdAsync()
    {
        using (tenantContext.BeginTenant(TestTenants.BootstrapTenantId))
        {
            return (await roleService.GetByNameAsync("Admin"))?.Id ?? Guid.Empty;
        }
    }

    /// <summary>
    /// Reads a tenant's administrator role by the name every tenant's system-created role carries.
    /// </summary>
    private async Task<Guid> ReadAdministratorRoleIdAsync(Guid tenantId)
    {
        using (tenantContext.BeginTenant(tenantId))
        {
            return (await roleService.GetByNameAsync("Admin"))?.Id ?? Guid.Empty;
        }
    }

    /// <summary>
    /// Reads the platform administrator role - the one belonging to no tenant, which is what makes it
    /// the mirror of a tenant's administrator role rather than one of them.
    /// </summary>
    private async Task<Guid> ReadPlatformAdministratorRoleIdAsync()
    {
        using (tenantContext.BeginPlatformScope())
        {
            return (await roleService.GetByNameAsync("Admin"))?.Id ?? Guid.Empty;
        }
    }

    /// <summary>
    /// Reads an account's identifier by username, so the seeder reports what the database holds rather
    /// than what it assumed it wrote.
    /// </summary>
    private async Task<Guid> ReadUserIdAsync(string username)
        => (await userService.GetByUsernameAsync(username))?.Id ?? Guid.Empty;
}
