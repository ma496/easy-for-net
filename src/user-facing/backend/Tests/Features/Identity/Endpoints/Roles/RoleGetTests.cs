using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;

namespace Backend.Tests.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleGetEndpoint"/> covering retrieval of existing, system-created
/// and non-existent roles.
/// </summary>
public class RoleGetTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that a created role can be retrieved by ID with the correct name and description.
    /// </summary>
    [Fact]
    public async Task Get_Role()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (createRsp, createRes) = await Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (getRsp, getRes) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.Name.Should().Be(request.Name);
        getRes.Description.Should().Be(request.Description);
        getRes.SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a role created by the seeder is reported as system-created so the UI can hide its actions.
    /// </summary>
    [Fact]
    public async Task Get_SystemCreated_Role()
    {
        await SetAuthTokenAsync();

        var (getRsp, getRes) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = TestRoles.AdminRoleId
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.SystemCreated.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that requesting a non-existent role returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_NonExistent_Role()
    {
        await SetAuthTokenAsync();

        var (getRsp, _) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Reads a role back straight from the database, so that what is asserted of it does not depend on
    /// the endpoint under test.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

    /// <summary>
    /// The permissions a role holds, read from the join rows rather than through the endpoint under test.
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
