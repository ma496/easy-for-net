using Backend.Features.Identity.Core;

namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// Tests for the <see cref="UserGetEndpoint"/> covering retrieval of existing and non-existent users.
/// </summary>
public class UserGetTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Verifies that a created user can be retrieved by ID with the correct username and email.
    /// </summary>
    [Fact]
    public async Task Get_User()
    {
        await SetAuthTokenAsync();

        var roleService = App.Services.GetRequiredService<IRoleService>();
        var faker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var request = faker.Generate();
        request.Roles = [TestRoles.TestRoleId];
        var (createRsp, createRes) = await App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (getRsp, getRes) = await App.Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.Username.Should().Be(request.Username);
        getRes.Email.Should().Be(request.Email);
        getRes.SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a user created by the seeder is reported as system-created so the UI can hide its actions.
    /// </summary>
    [Fact]
    public async Task Get_SystemCreated_User()
    {
        await SetAuthTokenAsync();

        var (getRsp, getRes) = await App.Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = TestUsers.AdminUserId
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.SystemCreated.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that requesting a non-existent user returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_NonExistent_User()
    {
        await SetAuthTokenAsync();

        var (getRsp, _) = await App.Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}