namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleGetEndpoint"/> covering retrieval of existing and non-existent roles.
/// </summary>
public class RoleGetTests(App app) : AppTestsBase(app)
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
        var (createRsp, createRes) = await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (getRsp, getRes) = await App.Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
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

        var (getRsp, getRes) = await App.Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
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

        var (getRsp, _) = await App.Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}