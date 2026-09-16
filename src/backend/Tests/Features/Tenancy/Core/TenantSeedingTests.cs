namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Tenancy;

/// <summary>
/// Tests for the state a freshly seeded database is in, and for what running the seeder again leaves
/// alone (AC-082, AC-083, AC-084, AC-121, AC-145).
/// </summary>
/// <remarks>
/// <para>
/// Every assertion here is about the baseline the seeder establishes rather than about anything a test
/// arranged: the bootstrap tenant, the account that administers both the platform and that tenant, and
/// the fact that a second run changes none of them. The suite runs against a database seeded once by
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
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row can
    /// relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// The normalized name of the role earlier versions granted to self-service sign-ups, which the
    /// seeder removes rather than reconciles.
    /// </summary>
    private const string PublicRoleNameNormalized = "public";

    /// <summary>
    /// The account the seeder creates, and the only account these tests name.
    /// </summary>
    private const string SeededAdministratorUsername = "admin";

    /// <summary>
    /// Verifies that exactly one system-created tenant exists, that it is the bootstrap tenant the
    /// migration and the seeder both name, and that the seeded administrator holds an active membership
    /// of it carrying tenant administration (AC-082).
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

        var administrator = await ReadSeededAdministratorAsync();

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
    /// Verifies that the seeded administrator is a platform administrator - that it holds
    /// <c>Platform.Administration</c>, and that the role granting it belongs to no tenant, so no tenant
    /// role could ever have conferred it (AC-083).
    /// </summary>
    [Fact]
    public async Task Seeded_Administrator_Is_A_Platform_Administrator()
    {
        var administrator = await ReadSeededAdministratorAsync();

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

        var grantingRoles = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.UserRoles.Any(assignment => assignment.UserId == administrator.Id)
                           && role.RolePermissions.Any(rolePermission => rolePermission.Permission.Name == Allow.Platform_Administration))
            .Select(role => new { role.Id, role.Name, role.TenantId })
            .ToListAsync(TestContext.Current.CancellationToken);

        grantingRoles.Should().NotBeEmpty("the administrator holds platform administration and some role has to grant it");
        grantingRoles.Should().OnlyContain(
            role => role.TenantId == null,
            "platform administration is granted only at platform scope, so no tenant role can confer it");
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
    /// Verifies that every role in the database declares a scope - either it belongs to a tenant, or it
    /// is a system-created platform role holding at least one platform permission - so there is no role
    /// that belongs nowhere and could be reached from anywhere (AC-121).
    /// </summary>
    /// <remarks>
    /// A role belonging to no tenant is exactly what a platform role is, and the only thing that makes
    /// one legitimate is holding authority that exists at platform scope alone. A role with no tenant and
    /// no platform permission is the shape a legacy seed left behind, and reading it would be neither
    /// restricted by a tenant nor justified by a platform grant.
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

        var platformPermissionNames = App.Services
            .GetRequiredService<IPermissionDefinitionService>()
            .GetPlatformPermissionNames();

        foreach (var platformRole in roles.Where(role => role.TenantId is null))
        {
            platformRole.SystemCreated.Should().BeTrue(
                $"the platform-scoped role '{platformRole.Name}' is declared by code, and a caller-created role belongs to the tenant it was made in");

            var heldPermissions = await ReadPermissionsHeldInRoleAsync(platformRole.Id);

            heldPermissions.Should().Contain(
                permissionName => platformPermissionNames.Contains(permissionName),
                $"the platform role '{platformRole.Name}' is what a platform grant is held through, so it has to hold at least one");
        }
    }

    /// <summary>
    /// Verifies that the role earlier versions granted to self-service sign-ups is gone, at every scope
    /// and retained rows included, and that signing up today grants no role at all - so nothing is handed
    /// to an account for the bare act of creating one (AC-145).
    /// </summary>
    /// <remarks>
    /// Both filters are relaxed for the retained read: the seeder removes the role with a hard delete
    /// precisely so that neither a live row nor a soft-deleted one goes on occupying the name.
    /// </remarks>
    [Fact]
    public async Task No_Public_Role_Is_Seeded()
    {
        var publicRoles = await DbContext.Roles
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .Where(role => role.NameNormalized == PublicRoleNameNormalized)
            .Select(role => new { role.Id, role.TenantId, role.IsDeleted })
            .ToListAsync(TestContext.Current.CancellationToken);

        publicRoles.Should().BeEmpty(
            "no role of that name exists at any scope, and the seeder removed it outright rather than merely retiring it");

        // The name being gone says nothing about the act, so the act is exercised: an account created
        // through sign-up has to come out holding no role and belonging to no tenant.
        var username = $"signedup-{Guid.NewGuid():N}";
        var (response, _) = await App.Client
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = "Signup#123",
                ConfirmPassword = "Signup#123"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var signedUp = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken);

        var heldRoles = await DbContext.UserRoles
            .AsNoTracking()
            .CountAsync(assignment => assignment.UserId == signedUp.Id, TestContext.Current.CancellationToken);

        heldRoles.Should().Be(0, "signing up grants an authenticated identity and nothing else");

        var memberships = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(membership => membership.UserId == signedUp.Id, TestContext.Current.CancellationToken);

        memberships.Should().Be(0, "and it places the account inside no tenant");
    }

    /// <summary>
    /// Reads the account the seeder creates, which every test here names by the username it is seeded
    /// under rather than by an identifier the suite would have to have captured first.
    /// </summary>
    /// <returns>The seeded administrator account.</returns>
    private async Task<User> ReadSeededAdministratorAsync()
        => await DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.UsernameNormalized == SeededAdministratorUsername, TestContext.Current.CancellationToken);

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
