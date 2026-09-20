namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Features.Tenancy.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Tenancy;

/// <summary>
/// Tests for the state a freshly seeded database is in, and for what running the seeder again leaves
/// alone (AC-082, AC-083, AC-084, AC-121).
/// </summary>
/// <remarks>
/// <para>
/// Every assertion here is about the baseline the seeder establishes rather than about anything a test
/// arranged: the bootstrap tenant, the account that administers that tenant, the separate account that
/// administers the platform, and the fact that a second run changes none of them. The suite runs against a database seeded once by
/// the shared fixture, so these are the only tests that may read rows they did not create - and they
/// read them, never write them.
/// </para>
/// <para>
/// Reconciliation is the reason the rest of the suite can run at all: the fixture migrates an empty
/// database and seeds it on every run, and the database is not wiped between runs, so a seeder that
/// duplicated what it found, or that reset an identifier, would corrupt the baseline the previous run
/// left behind. The test below proves the three kinds a tenant depends on - the tenant, the membership
/// that places an account in it, and the role that carries authority inside it - all survive a second
/// run with the same identities.
/// </para>
/// </remarks>
public class TenantSeedingTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that exactly one system-created tenant exists, that it is the bootstrap tenant the
    /// migration and the seeder both name, and that the seeded tenant administrator holds an active
    /// membership of it carrying tenant administration (AC-082).
    /// </summary>
    [Fact]
    public async Task Bootstrap_Tenant_Exists_With_The_Seeded_Administrator()
    {
        var systemCreatedTenants = await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(tenant => tenant.SystemCreated)
            .Select(tenant => new { tenant.Id, tenant.Identifier, tenant.Status })
            .ToListAsync(TestContext.Current.CancellationToken);

        systemCreatedTenants.Should().ContainSingle(
            "the bootstrap tenant is the one tenant the platform creates for itself; every other tenant is created by a caller");

        systemCreatedTenants[0].Id.Should().Be(TenancyConstants.BootstrapTenantId);
        systemCreatedTenants[0].Identifier.Should().Be(TenancyConstants.BootstrapTenantIdentifier);
        systemCreatedTenants[0].Status.Should().Be(TenantStatus.Active);

        var administrator = await ReadSeededAccountAsync(TestUsers.TenantAdminUsername);

        var membership = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TenantId == TenancyConstants.BootstrapTenantId
                             && candidate.UserId == administrator.Id,
                TestContext.Current.CancellationToken);

        membership.Should().NotBeNull("the seeder places the administrator inside the bootstrap tenant rather than only creating it");
        membership!.IsDeleted.Should().BeFalse("a removed membership is a soft-deleted one, and this one stands");

        var heldPermissions = await ReadPermissionsHeldInTenantAsync(administrator.Id, TenancyConstants.BootstrapTenantId);

        heldPermissions.Should().Contain(
            Allow.TenantMember_UpdateRoles,
            "replacing a member's role assignments is what tenant administration is, and the bootstrap tenant has to have somebody able to do it");
    }

    /// <summary>
    /// Verifies that the seeded platform administrator is a platform administrator - that it holds
    /// the platform tier, and that the role granting its platform-scoped permissions belongs to no tenant, so no tenant
    /// role could ever have conferred it (AC-083) - and that it belongs to no tenant itself, the
    /// bootstrap tenant being administered by an account of its own.
    /// </summary>
    [Fact]
    public async Task Seeded_Administrator_Is_A_Platform_Administrator()
    {
        var administrator = await ReadSeededAccountAsync(TestUsers.PlatformAdminUsername);

        var platformRoleIds = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == null)
            .Select(role => role.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var heldPlatformRoleIds = await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == administrator.Id && platformRoleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.RoleId)
            .ToListAsync(TestContext.Current.CancellationToken);

        heldPlatformRoleIds.Should().Contain(
            TestRoles.PlatformAdminRoleId,
            "the seeded administrator holds the platform's own administrator role, which is the one belonging to no tenant");

        administrator.IsPlatform.Should().BeTrue(
            "the seeded platform account is marked as belonging to the platform tier, which is what admits it to platform scope");

        var grantingRoles = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.UserRoles.Any(assignment => assignment.UserId == administrator.Id)
                           && role.RolePermissions.Any(rolePermission => rolePermission.Permission.Scope == PermissionScope.Platform))
            .Select(role => new { role.Id, role.Name, role.TenantId })
            .ToListAsync(TestContext.Current.CancellationToken);

        grantingRoles.Should().NotBeEmpty("the administrator holds platform-scoped permissions and some role has to grant them");
        grantingRoles.Should().OnlyContain(
            role => role.TenantId == null,
            "a platform-scoped permission is granted only at platform scope, so no tenant role can confer it");

        var memberships = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == administrator.Id)
            .Select(membership => membership.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

        memberships.Should().BeEmpty(
            "the platform administrator is not the bootstrap tenant's administrator - that tenant is seeded with an account of its own");
    }

    /// <summary>
    /// Verifies that running the seeder again preserves a tenant, a membership and a tenant role that
    /// already exist, identities and permissions included - which is what makes it safe on the template's
    /// own first start and on every start after it (AC-084).
    /// </summary>
    /// <remarks>
    /// The second run is resolved from a scope of its own, so it is an independent unit of work rather
    /// than a second pass over the change tracker the test arranged through - the same independence the
    /// seeder's own startup run has from everything that came before it.
    /// </remarks>
    [Fact]
    public async Task Reconciliation_Preserves_Tenants_Memberships_And_Tenant_Roles()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);

        using (var scope = App.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
        }

        var preservedTenant = await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == tenant.Id, TestContext.Current.CancellationToken);

        preservedTenant.Should().NotBeNull("reconciliation deletes no tenant");
        preservedTenant!.IsDeleted.Should().BeFalse();
        preservedTenant.Identifier.Should().Be(tenant.Identifier, "the identity a caller addresses a tenant by is not rewritten");

        var preservedMembership = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TenantId == tenant.Id && candidate.UserId == member.Id,
                TestContext.Current.CancellationToken);

        preservedMembership.Should().NotBeNull("reconciliation writes the memberships it is missing, and leaves the ones already there");
        preservedMembership!.IsDeleted.Should().BeFalse();

        var preservedRole = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == roleId, TestContext.Current.CancellationToken);

        preservedRole.Should().NotBeNull("reconciliation deletes no role");
        preservedRole!.TenantId.Should().Be(tenant.Id, "a role belongs to the tenant it was created in");
        preservedRole.IsDeleted.Should().BeFalse();

        var preservedPermissions = await ReadPermissionsHeldInRoleAsync(roleId);

        preservedPermissions.Should().Equal(
            [Allow.Tenant_View],
            "a role keeps exactly the permissions it held: reconciliation adds what the catalogue gained and removes what it lost, and neither happened here");
    }

    /// <summary>
    /// Verifies that every role in the database declares a scope - it belongs to a tenant, or it is a
    /// platform role - and that every system-created platform role holds at least one platform
    /// permission, so no role left behind by a legacy seed survives (AC-121).
    /// </summary>
    /// <remarks>
    /// A role belonging to no tenant is exactly what a platform role is. A platform administrator acting
    /// in no tenant creates such roles on purpose, so a caller-created one is legitimate whatever it
    /// holds. A system-created role with no tenant and no platform permission is the shape a legacy seed
    /// left behind, and reading it would be neither restricted by a tenant nor justified by a platform
    /// grant.
    /// </remarks>
    [Fact]
    public async Task Every_Role_Has_A_Declared_Scope()
    {
        var roles = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Select(role => new { role.Id, role.Name, role.SystemCreated, role.TenantId })
            .ToListAsync(TestContext.Current.CancellationToken);

        roles.Should().NotBeEmpty("the seeder reconciles an administrator role at each scope, so the database holds roles");

        var platformPermissionNames = PlatformOnlyPermissionNames();

        foreach (var platformRole in roles.Where(role => role.TenantId is null && role.SystemCreated))
        {
            var heldPermissions = await ReadPermissionsHeldInRoleAsync(platformRole.Id);

            heldPermissions.Should().Contain(
                permissionName => platformPermissionNames.Contains(permissionName),
                $"the platform role '{platformRole.Name}' is what a platform grant is held through, so it has to hold at least one");
        }
    }

    /// <summary>
    /// Verifies that the seeded tenant administrator holds exactly one active membership, which is what
    /// lets sign-in resolve a tenant for it without being told which one.
    /// </summary>
    /// <remarks>
    /// This was a convenience and is now a requirement. An ordinary account holding anything other than
    /// one active membership is refused at sign-in and asked to name a tenant, so a seeder that gave
    /// this account a second membership would not fail here alone - it would fail every test in the
    /// suite that signs in with the default credentials, with a refusal that says nothing about the
    /// seed. Asserting it here is what turns that into one legible failure.
    /// </remarks>
    [Fact]
    public async Task Seeded_Tenant_Administrator_Holds_Exactly_One_Membership()
    {
        var administrator = await ReadSeededAccountAsync(TestUsers.TenantAdminUsername);

        var tenantIds = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == administrator.Id
                                 && DbContext.Tenants.Any(tenant => tenant.Id == membership.TenantId && tenant.Status == TenantStatus.Active))
            .Select(membership => membership.TenantId)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        tenantIds.Should().ContainSingle(
            "sign-in resolves a tenant for an ordinary account only when exactly one active membership stands, and every test signing in with the default credentials depends on it")
            .Which.Should().Be(TestTenants.BootstrapTenantId, "and it is the tenant the seeder puts this account in");
    }

    /// <summary>
    /// Reads an account the seeder creates, which every test here names by the username it is seeded
    /// under rather than by an identifier the suite would have to have captured first.
    /// </summary>
    /// <param name="username">The username the account is seeded under.</param>
    /// <returns>The seeded account.</returns>
    private async Task<User> ReadSeededAccountAsync(string username)
        => await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

    /// <summary>
    /// Reads the permissions one account holds through the roles that belong to one tenant, so what is
    /// reported is that account's authority <em>there</em> rather than everywhere it holds any.
    /// </summary>
    /// <param name="userId">The account whose authority in the tenant is read.</param>
    /// <param name="tenantId">The tenant the authority has to be held in.</param>
    /// <returns>The names of the permissions held in that tenant.</returns>
    private async Task<List<string>> ReadPermissionsHeldInTenantAsync(Guid userId, Guid tenantId)
    {
        var roleIds = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId)
            .Select(role => role.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var heldRoleIds = await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && roleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.RoleId)
            .ToListAsync(TestContext.Current.CancellationToken);

        heldRoleIds.Should().NotBeEmpty(
            "the account holds no role in the tenant, so it could hold no authority there");

        return await DbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => heldRoleIds.Contains(rolePermission.RoleId))
            .Select(rolePermission => rolePermission.Permission.Name)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads the permission names one role holds.
    /// </summary>
    /// <param name="roleId">The role being read.</param>
    /// <returns>The names of the permissions the role holds.</returns>
    private async Task<List<string>> ReadPermissionsHeldInRoleAsync(Guid roleId)
        => await DbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.Permission.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
}
