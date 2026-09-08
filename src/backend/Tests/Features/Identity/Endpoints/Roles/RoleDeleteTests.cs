namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleDeleteEndpoint"/> covering deletion of roles, non-existent roles, and protected system-created roles.
/// </summary>
public class RoleDeleteTests(App app) : AppTestsBase(app)
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
        var (createRsp, createRes) = await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then delete the role
        var (deleteRsp, deleteRes) = await App.Client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(
            new()
            {
                Id = createRes.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteRes.Success.Should().BeTrue();

        // Verify the role is deleted by trying to get it
        var (getRsp, _) = await App.Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
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

        var (deleteRsp, _) = await App.Client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(
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

        // Get a system-created role (Test role from seeder)
        var roleService = App.Services.GetRequiredService<IRoleService>();
        var systemCreatedRole = await roleService.GetByNameAsync("Admin");
        systemCreatedRole.Should().NotBeNull();

        // Try to delete the system-created role
        var (deleteRsp, res) = await App.Client.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, ProblemDetails>(
            new()
            {
                Id = systemCreatedRole!.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeDeleted);
    }
}