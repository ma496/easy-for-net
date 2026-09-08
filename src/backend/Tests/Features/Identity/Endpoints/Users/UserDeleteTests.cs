namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// Tests for the <see cref="UserDeleteEndpoint"/> covering deletion of users, non-existent users, and protected system-created users.
/// </summary>
public class UserDeleteTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Verifies that a created user can be successfully deleted and subsequent GET returns 404.
    /// </summary>
    [Fact]
    public async Task Delete_User()
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

        // Then delete the user
        var (deleteRsp, deleteRes) = await App.Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(
            new()
            {
                Id = createRes.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteRes.Success.Should().BeTrue();

        // Verify user is deleted by trying to get it
        var (getRsp, _) = await App.Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that deleting a non-existent user returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Delete_NonExistent_User()
    {
        await SetAuthTokenAsync();

        var (deleteRsp, _) = await App.Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that attempting to delete the system-created admin user returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedUserCannotBeDeleted"/>.
    /// </summary>
    [Fact]
    public async Task Cannot_Delete_Admin_User()
    {
        await SetAuthTokenAsync();

        // Get the system-created user (admin from seeder)
        var userService = App.Services.GetRequiredService<IUserService>();
        var systemCreatedUser = await userService.GetByUsernameAsync("admin");
        systemCreatedUser.Should().NotBeNull();

        // Try to delete the system-created user
        var (deleteRsp, res) = await App.Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, ProblemDetails>(
            new()
            {
                Id = systemCreatedUser!.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedUserCannotBeDeleted);
    }
}