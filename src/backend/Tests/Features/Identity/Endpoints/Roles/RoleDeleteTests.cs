namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="RoleDeleteEndpoint"/> covering deletion of roles, non-existent roles,
/// protected system-created roles - the platform role (AC-043) and a tenant's own (AC-043) - and the
/// role of another tenant that cannot be deleted (AC-111).
/// </summary>
public class RoleDeleteTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a newly created role can be successfully deleted and subsequent GET returns 404.
    /// </summary>
    [Fact]
    public async Task Delete_Role()
    {
        await SetAuthTokenAsync();

        // First create a role
        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (createRsp, createRes) = await Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then delete the role
        var (deleteRsp, deleteRes) = await Client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(
            new()
            {
                Id = createRes.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteRes.Success.Should().BeTrue();

        // Verify the role is deleted by trying to get it
        var (getRsp, _) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that attempting to delete a non-existent role returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Delete_NonExistent_Role()
    {
        await SetAuthTokenAsync();

        var (deleteRsp, _) = await Client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that attempting to delete the system-created Admin role returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedRoleCannotBeDeleted"/>.
    /// </summary>
    [Fact]
    public async Task Cannot_Delete_Admin_Role()
    {
        await SetAuthTokenAsync();

        // The bootstrap tenant's administrator role, read across tenants because the row carries the
        // tenant it belongs to and the lookup here is the seeder's rather than a caller's.
        var systemCreatedRole = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == TestRoles.AdminRoleId, TestContext.Current.CancellationToken);
        systemCreatedRole.SystemCreated.Should().BeTrue("the premise of this test is that the role is one the seeder made");

        // Try to delete the system-created role
        var (deleteRsp, res) = await Client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, ProblemDetails>(
            new()
            {
                Id = systemCreatedRole.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeDeleted);
    }

    /// <summary>
    /// Verifies that a tenant's own system-created administrator role cannot be deleted (AC-043).
    /// </summary>
    /// <remarks>
    /// The role is the one provisioning gave the tenant when it was created, and the caller is a member
    /// of that very tenant holding the delete permission - so what refuses the request is the role's
    /// origin rather than a reach the caller lacked. The role is read back afterwards both through the
    /// endpoint and from the database: a soft delete leaves the row in place, so only a read that
    /// resolves the role again tells "refused" apart from "deleted and no longer shown".
    /// </remarks>
    [Fact]
    public async Task Cannot_Delete_System_Created_Tenant_Role()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_Delete, Allow.Role_View));
        var administratorRoleId = await AdministratorRoleIdAsync(tenant.Id);

        var client = await ClientForAsync(administrator.Username);

        var (deleteRsp, problem) = await client
            .DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, ProblemDetails>(new() { Id = administratorRoleId });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeDeleted);

        var (getRsp, resolved) = await client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = administratorRoleId });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        resolved.SystemCreated.Should().BeTrue("the role is the tenant's administrator role, and it is still the one it was");

        // The row itself is still live: a soft delete would leave it in the table and hide it from every
        // read, so the read that resolves the role has to be made with the soft-delete filter in force -
        // which is what an unfiltered tenant read is here.
        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(role => role.Id == administratorRoleId, TestContext.Current.CancellationToken);
        stored.Should().NotBeNull("the refusal is the whole outcome of the request, so nothing was written");
    }

    /// <summary>
    /// Verifies that deleting a role belonging to another tenant answers exactly as deleting a role that
    /// does not exist does, and leaves it in place (AC-111).
    /// </summary>
    /// <remarks>
    /// The role is read back across every tenant with the soft-delete filter in force, so the row being
    /// found says the delete did not reach it - a delete that had run and been refused later would leave
    /// a soft-deleted role whose next read would report it as missing, which is the very answer the
    /// caller was given and would make the refusal indistinguishable from the outcome it refused.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_Role_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.Role_Delete));
        var stranger = await CreateTenantRoleAsync(other.Id, Allow.Role_View);

        var before = await RoleAsync(stranger);
        var beforePermissions = await RolePermissionIdsAsync(stranger);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(new() { Id = stranger });

        var (unknown, _) = await client
            .DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(new() { Id = Guid.NewGuid() });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "a role of another tenant and a role that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes a role the caller may not delete from one that is not there");

        var after = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(role => role.Id == stranger, TestContext.Current.CancellationToken);

        after.Should().NotBeNull("the role belongs to its own tenant and was left exactly where it was");
        after!.Name.Should().Be(before.Name, "and still holds the name it was given");
        (await RolePermissionIdsAsync(stranger)).Should().BeEquivalentTo(beforePermissions,
            "and still grants exactly what it granted");
    }

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
    /// Reads a role back across every tenant, because what is asserted of it is a row a caller acting in
    /// another tenant must have been unable to touch.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

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
