namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="ChangePermissionsEndpoint"/> covering assigning permissions to roles, the
/// platform permission a tenant role can never be given, the tenant-only permission a platform
/// role can never be given, the system-created tenant role whose
/// permissions cannot be changed at all, and the role of another tenant that cannot be reached
///.
/// </summary>
public class ChangePermissionsTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that permissions can be successfully assigned to a newly created role.
    /// </summary>
    /// <remarks>
    /// The role belongs to a tenant this test made and the caller administers it from inside, because a
    /// role belongs to a tenant and only the tenant-tier permissions of the catalogue can be granted
    /// through one - so the set assigned here is drawn from that tier rather than from whatever
    /// the catalogue happens to return first.
    /// </remarks>
    [Fact]
    public async Task Change_Permissions()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_ChangePermissions));
        var roleId = await CreateTenantRoleAsync(tenant.Id);
        var permissions = await TenantPermissionIdsAsync(skip: 0, take: 2);

        var client = await ClientForAsync(administrator.Username);

        var (rsp, res) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = roleId,
                Permissions = permissions
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Permissions.Should().BeEquivalentTo(permissions);
        (await RolePermissionIdsAsync(roleId)).Should().BeEquivalentTo(permissions,
            "the response and the stored set are the same set, so the echoed identifiers are the ones the role was given");
    }

    /// <summary>
    /// Verifies that permissions can be updated on a role that already has permissions, replacing the existing set with a mix of old and new permissions.
    /// </summary>
    /// <remarks>
    /// The first request establishes the state the second one is about: the role has permissions of its
    /// own by the time the mix is asked for, so the replacement is a real one - an old permission is kept
    /// while the rest of the set changes, which a change that only ever added could not produce.
    /// </remarks>
    [Fact]
    public async Task Change_Permissions_Of_Role_Already_Has_Permissions()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_ChangePermissions));
        var roleId = await CreateTenantRoleAsync(tenant.Id);

        var client = await ClientForAsync(administrator.Username);
        var permissions = await TenantPermissionIdsAsync(skip: 0, take: 2);

        var (rsp, _) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = roleId,
                Permissions = permissions
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK, "the role holds permissions of its own before the set is mixed");

        var mixPermissions = new List<Guid> { permissions[0] };
        mixPermissions.AddRange(await TenantPermissionIdsAsync(skip: 2, take: 2));

        var (rsp1, res1) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = roleId,
                Permissions = mixPermissions
            });

        rsp1.StatusCode.Should().Be(HttpStatusCode.OK);
        res1.Permissions.Should().BeEquivalentTo(mixPermissions);

        (await RolePermissionIdsAsync(roleId)).Should().BeEquivalentTo(mixPermissions,
            "the set asked for replaces the one the role had, rather than being added to it");
    }

    /// <summary>
    /// Verifies that a platform-tier permission cannot be granted through a role belonging to a tenant,
    /// and that the refusal leaves the role's permission set exactly as it was.
    /// </summary>
    /// <remarks>
    /// The permission is declared platform-scoped in code, which is asserted here as the premise the refusal
    /// rests on rather than assumed: it governs the installation rather than any one tenant, so granting it
    /// through a tenant's own role would be a tenant promoting itself to platform authority. The refusal is
    /// raised over the whole requested set rather than the additions alone, so the role keeps the set it
    /// had - which is what the comparison afterwards says, and it says more than "the platform permission
    /// was not added": nothing else was added or removed either.
    /// </remarks>
    [Fact]
    public async Task Platform_Permission_Cannot_Be_Granted_To_A_Tenant_Role()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_ChangePermissions));
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        PlatformOnlyPermissionNames()
            .Should()
            .Contain(Allow.Tenant_Create, "the premise of this test is that this permission governs the installation rather than a tenant");

        var platformPermissionId = await PermissionIdAsync(Allow.Tenant_Create);
        var before = await RolePermissionIdsAsync(roleId);
        before.Should().NotContain(platformPermissionId, "the role does not hold it to begin with");

        var client = await ClientForAsync(administrator.Username);

        var (refused, problem) = await client
            .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ProblemDetails>(new()
            {
                Id = roleId,
                Permissions = [platformPermissionId]
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.PlatformPermissionNotGrantable);
        problem.Errors.First().Name.Should().Be("permissions", "the caller is told which field to change");

        (await RolePermissionIdsAsync(roleId)).Should().BeEquivalentTo(before,
            "the requested set is refused whole, so the role ends up with exactly the permissions it had");
    }

    /// <summary>
    /// Verifies that a permission exercisable only inside a tenant cannot be granted through a platform
    /// role: a platform role counts only in platform scope, so the grant would confer nothing anywhere.
    /// The requested set is refused whole and the role keeps what it had.
    /// </summary>
    /// <remarks>
    /// The catalogue this template declares holds no permission exercisable only inside a tenant - every
    /// one a tenant uses is declared for both scopes - so the test stores one of its own and removes it
    /// afterwards. The seeder reconciles the permission rows only at startup, so the row is not
    /// reconciled away while the test runs.
    /// </remarks>
    [Fact]
    public async Task Tenant_Permission_Cannot_Be_Granted_To_A_Platform_Role()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (created, platformRole) = await Client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = $"Platform {Guid.NewGuid():N}" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenantOnlyPermission = new Permission
        {
            Name = $"Test.TenantOnly.{Guid.NewGuid():N}",
            DisplayName = "Tenant only",
            Scope = PermissionScope.Tenant
        };
        DbContext.Permissions.Add(tenantOnlyPermission);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var before = await RolePermissionIdsAsync(platformRole.Id);

            var (refused, problem) = await Client
                .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ProblemDetails>(new()
                {
                    Id = platformRole.Id,
                    Permissions = [tenantOnlyPermission.Id]
                });

            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            problem.Errors.Should().ContainSingle();
            problem.Errors.First().Code.Should().Be(ErrorCodes.TenantPermissionNotGrantable);
            problem.Errors.First().Name.Should().Be("permissions", "the caller is told which field to change");

            (await RolePermissionIdsAsync(platformRole.Id)).Should().BeEquivalentTo(before,
                "the requested set is refused whole, so the role ends up with exactly the permissions it had");
        }
        finally
        {
            DbContext.Permissions.Remove(tenantOnlyPermission);
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Verifies that the permissions of a tenant's own system-created administrator role cannot be
    /// changed, and that the role keeps them.
    /// </summary>
    /// <remarks>
    /// The request asks for the empty set, so the change would strip the administrator role of every
    /// permission it holds - the state a tenant could not be recovered from from inside itself. The
    /// refusal is asserted as the code it carries and as the permission set it left behind, which is
    /// compared before and after rather than only checked for emptiness: a partial write that had removed
    /// a few of the permissions would otherwise read as a refusal too.
    /// </remarks>
    [Fact]
    public async Task Cannot_Change_System_Created_Tenant_Role_Permissions()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_ChangePermissions));
        var administratorRoleId = await AdministratorRoleIdAsync(tenant.Id);

        var before = await RolePermissionIdsAsync(administratorRoleId);
        before.Should().NotBeEmpty("provisioning gives the tenant's administrator role the tenant-tier permissions, so there is a set to lose");

        var client = await ClientForAsync(administrator.Username);

        var (refused, problem) = await client
            .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ProblemDetails>(new()
            {
                Id = administratorRoleId,
                Permissions = []
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRolePermissionsCannotBeChanged);

        (await RolePermissionIdsAsync(administratorRoleId)).Should().BeEquivalentTo(before,
            "the administrator role still holds every permission it held, so the tenant is still administrable from inside itself");
    }

    /// <summary>
    /// Verifies that changing the permissions of a role belonging to another tenant answers exactly as
    /// changing the permissions of a role that does not exist does, and leaves it as it was.
    /// </summary>
    /// <remarks>
    /// The set asked for is one the caller could grant inside its own tenant - a tenant-tier permission -
    /// so the tenant the role belongs to is the only reason the request is refused. The role's permission
    /// set is compared afterwards, because the two halves of the claim are separate: the answer must be
    /// indistinguishable from a miss, and the role must still grant exactly what it did.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_Role_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.Role_ChangePermissions));
        var stranger = await CreateTenantRoleAsync(other.Id, Allow.Role_View);

        var grantedPermissionId = await PermissionIdAsync(Allow.Role_View);
        var before = await RolePermissionIdsAsync(stranger);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(new()
            {
                Id = stranger,
                Permissions = [grantedPermissionId, await PermissionIdAsync(Allow.Role_Update)]
            });

        var (unknown, _) = await client
            .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(new()
            {
                Id = Guid.NewGuid(),
                Permissions = [grantedPermissionId, await PermissionIdAsync(Allow.Role_Update)]
            });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "a role of another tenant and a role that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes a role the caller may not re-permission from one that is not there");

        (await RolePermissionIdsAsync(stranger)).Should().BeEquivalentTo(before,
            "the role of another tenant still grants exactly what it granted");
    }

    /// <summary>
    /// A page of the permissions a tenant role may hold, ordered by name so that two calls with
    /// different offsets never return the same permission - which is what lets a test keep one and
    /// replace the rest. Platform-scoped permissions are left out because a tenant's role can never be
    /// granted one, so they are not part of any set this endpoint would accept.
    /// </summary>
    /// <param name="skip">How many of the scope's permissions to pass over.</param>
    /// <param name="take">How many to take.</param>
    /// <returns>The identifiers of the permissions.</returns>
    private async Task<List<Guid>> TenantPermissionIdsAsync(int skip, int take)
    {
        return await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Scope != PermissionScope.Platform)
            .OrderBy(permission => permission.Name)
            .Skip(skip)
            .Take(take)
            .Select(permission => permission.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The stored permission behind a permission name, looked up from the catalogue so that the test
    /// grants by identifier - which is what the request carries - rather than by a name the endpoint
    /// never sees.
    /// </summary>
    /// <param name="permissionName">The permission's name in the code-declared catalogue.</param>
    /// <returns>The identifier of the stored permission.</returns>
    private async Task<Guid> PermissionIdAsync(string permissionName)
        => await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Name == permissionName)
            .Select(permission => permission.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The administrator role provisioning gave a tenant, read inside the tenant's own scope so that the
    /// role found is the one belonging to that tenant rather than to the platform.
    /// </summary>
    /// <param name="tenantId">The tenant whose administrator role is wanted.</param>
    /// <returns>The identifier of the tenant's system-created administrator role.</returns>
    private async Task<Guid> AdministratorRoleIdAsync(Guid tenantId)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        return await DbContext.Roles
            .AsNoTracking()
            .Where(role => role.SystemCreated && role.Name == "Admin")
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The permissions a role holds, read from the join rows rather than through any surface that
    /// narrows to the tenant being acted in.
    /// </summary>
    /// <param name="roleId">The role whose permissions are wanted.</param>
    /// <returns>The permission identifiers the role holds.</returns>
    private async Task<List<Guid>> RolePermissionIdsAsync(Guid roleId)
        => await DbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
}