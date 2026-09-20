namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the reach the platform tier gives the role endpoints: the reader, the rename, the
/// permission change and the delete all act on a role belonging to a tenant the caller holds no
/// membership of, once it has entered that tenant (AC-113).
/// </summary>
/// <remarks>
/// <para>
/// Entering is how the reach is exercised, and the only way: a session carries the permissions of the
/// scope it acts in, so a platform caller inside tenant A is an actor of tenant A and reaches nothing of
/// tenant B - the last test here states that directly. What the tier gives is admission to any tenant
/// without a membership, and inside it the tenant's own authority; it is not a standing that spans
/// tenants at once.
/// </para>
/// <para>
/// Every test here signs in as a platform account and enters the tenant it is about to act on, through
/// <see cref="SignInAsPlatformAdministratorAsync"/>. The premise that it belongs to no tenant at all is
/// asserted rather than assumed, because it is the whole of what these tests are about: a caller who
/// happened to be a member would be reaching the role as a member, and the reach would be untested.
/// </para>
/// <para>
/// The role acted on is one the test made, so it is not system-created and each of the four operations
/// is available to it. A tenant's own administrator role refuses the rename, the permission change and
/// the delete outright, which would make a test of the widening over it pass for the wrong reason.
/// </para>
/// </remarks>
public class RolePlatformAdministrationTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see the role a delete
    /// retained can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that a platform administrator reads a role of a tenant it holds no membership of, with
    /// the permissions the role actually holds (AC-113).
    /// </summary>
    [Fact]
    public async Task Reads_A_Role_Of_A_Tenant_It_Does_Not_Belong_To()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View, Allow.User_View);
        var grantedPermissionIds = await RolePermissionIdsAsync(roleId);

        await SignInAsPlatformAdministratorAsync(tenant.Id);

        var (response, role) = await Client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = roleId });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the caller administers the installation rather than this tenant");
        role.Id.Should().Be(roleId);
        role.Permissions.Should().BeEquivalentTo(grantedPermissionIds, "the role is read as it is, not as an empty one");
    }

    /// <summary>
    /// Verifies that a platform administrator renames a role of a tenant it holds no membership of, and
    /// that the rename reaches the stored row (AC-113).
    /// </summary>
    [Fact]
    public async Task Renames_A_Role_Of_A_Tenant_It_Does_Not_Belong_To()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var newName = $"role-{Guid.NewGuid():N}";

        await SignInAsPlatformAdministratorAsync(tenant.Id);

        var (response, updated) = await Client
            .PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(new()
            {
                Id = roleId,
                Name = newName,
                Description = "Renamed by a platform administrator from outside the tenant"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Name.Should().Be(newName);

        (await RoleAsync(roleId)).Name.Should().Be(newName, "the rename reached the row the tenant owns, not a copy of it");
    }

    /// <summary>
    /// Verifies that a platform administrator replaces the permission set of a role belonging to a
    /// tenant it holds no membership of, and that the tenant's role grants the new set afterwards
    /// (AC-113).
    /// </summary>
    [Fact]
    public async Task Changes_The_Permissions_Of_A_Role_Of_A_Tenant_It_Does_Not_Belong_To()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var requested = await TenantPermissionIdsAsync(take: 2);

        await SignInAsPlatformAdministratorAsync(tenant.Id);

        var (response, changed) = await Client
            .PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(new()
            {
                Id = roleId,
                Permissions = requested
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        changed.Permissions.Should().BeEquivalentTo(requested);
        (await RolePermissionIdsAsync(roleId))
            .Should().BeEquivalentTo(requested, "the set the tenant's role grants is the one the platform administrator asked for");
    }

    /// <summary>
    /// Verifies that a platform administrator deletes a role belonging to a tenant it holds no
    /// membership of, and that the delete is the soft one every other caller gets: the row is retained
    /// carrying the deletion, and the tenant stops being shown the role (AC-113).
    /// </summary>
    [Fact]
    public async Task Deletes_A_Role_Of_A_Tenant_It_Does_Not_Belong_To()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        await SignInAsPlatformAdministratorAsync(tenant.Id);

        var (response, deleted) = await Client
            .DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(new() { Id = roleId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        deleted.Success.Should().BeTrue();

        var retained = await DbContext.Roles
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

        retained.IsDeleted.Should().BeTrue("the delete is recorded on the row rather than performed on it");
        retained.DeletedAt.Should().NotBeNull("and it is recorded with the time it happened");
        retained.TenantId.Should().Be(tenant.Id, "the row stays where it belongs, so the tenant's name for it stays reserved");

        (await RolesVisibleInAsync(tenant.Id)).Should().NotContain(
            role => role.Id == roleId,
            "the tenant no longer lists the role, which is what makes the delete mean anything to it");
    }

    /// <summary>
    /// Signs in as a platform account, has it enter the tenant named, and proves the premise every test
    /// here rests on: that the tenant is one the caller holds no membership of, so what the requests
    /// below reach is reached by the tier that admitted it rather than by belonging there.
    /// </summary>
    /// <param name="tenantId">The tenant the caller enters and must not belong to.</param>
    private async Task SignInAsPlatformAdministratorAsync(Guid tenantId)
    {
        var administrator = await SignInAsPlatformAdministratorEnteringAsync(tenantId);

        (await MembershipService.IsMemberAsync(tenantId, administrator.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse("the caller works inside a tenant it does not belong to, which is the standing these tests are stated over");
    }

    /// <summary>
    /// Verifies that the reach stops at the tenant entered: a platform caller acting inside one tenant
    /// is answered about a role of another exactly as it would be about a role that does not exist
    /// (AC-111, AC-113).
    /// </summary>
    /// <remarks>
    /// This is the other half of the four tests above, and it is what makes them say something. A
    /// standing that spanned every tenant at once would pass all four and this one too by simply never
    /// narrowing; what is actually being shown is that entering a tenant is what opens it, so a caller
    /// that entered somewhere else is outside.
    /// </remarks>
    [Fact]
    public async Task Does_Not_Reach_A_Role_Of_A_Tenant_It_Has_Not_Entered()
    {
        var entered = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(other.Id, Allow.Role_View);

        await SignInAsPlatformAdministratorAsync(entered.Id);

        var (response, _) = await Client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = roleId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "acting inside a tenant is acting inside that tenant, so another tenant's role is absent exactly as a role that never existed is");
    }

    /// <summary>
    /// Reads a role back across every tenant, because what is asserted of it is a row a caller acting in
    /// another tenant has just changed.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

    /// <summary>
    /// The roles a tenant lists to itself, read under a scope of that tenant so the soft-delete filter
    /// is in force exactly as it is for a caller acting there.
    /// </summary>
    /// <param name="tenantId">The tenant whose roles are read.</param>
    /// <returns>The roles the tenant lists.</returns>
    private async Task<List<Role>> RolesVisibleInAsync(Guid tenantId)
    {
        var roles = new List<Role>();

        await TenantScopedAsync(tenantId, async () => roles = await DbContext.Roles
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));

        return roles;
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

    /// <summary>
    /// Identifiers of permissions a tenant's own role may hold - everything but the platform scope,
    /// drawn in a stable order so a test can name a set without depending on what happens to be stored
    /// first.
    /// </summary>
    /// <param name="take">How many to read.</param>
    /// <returns>The permission identifiers.</returns>
    private async Task<List<Guid>> TenantPermissionIdsAsync(int take)
    {
        return await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Scope != PermissionScope.Platform)
            .OrderBy(permission => permission.Name)
            .Take(take)
            .Select(permission => permission.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
