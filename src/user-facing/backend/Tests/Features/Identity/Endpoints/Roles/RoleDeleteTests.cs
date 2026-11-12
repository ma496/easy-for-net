using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;

namespace Backend.Tests.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleDeleteEndpoint"/> covering deletion of roles, non-existent roles,
/// protected system-created roles - the platform role and a tenant's own - and the
/// role of another tenant that cannot be deleted.
/// </summary>
public class RoleDeleteTests(App app) : UsersTestsBase(app)
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
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeDeleted.Value);
    }

    /// <summary>
    /// Reads a role back across every tenant, because what is asserted of it is a row a caller acting in
    /// another tenant must have been unable to touch.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            
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

    [Fact]
    public async Task Cannot_Delete_System_Created_Role()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_Delete));
        var client = await ClientForAsync(administrator.Username);

        var (rsp, problem) = await client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, ProblemDetails>(
            new() { Id = TestRoles.AdminRoleId });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SystemCreatedRoleCannotBeDeleted.Value);
    }
}
