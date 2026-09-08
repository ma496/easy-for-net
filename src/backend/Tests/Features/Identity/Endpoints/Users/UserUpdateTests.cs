namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// Tests for the <see cref="UserUpdateEndpoint"/> covering updating users, non-existent users, and protected system-created users.
/// </summary>
public class UserUpdateTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Verifies that a created user can be successfully updated with new first name, last name, active status, and roles.
    /// </summary>
    [Fact]
    public async Task Update_User()
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

        var updateFaker = new Faker<UserUpdateRequest>()
            .RuleFor(u => u.Id, f => createRes.Id)
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => false);
        var updateRequest = updateFaker.Generate();
        updateRequest.Roles = [TestRoles.TestOneRoleId, TestRoles.TestTwoRoleId];
        var (updateRsp, updateRes) = await App.Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(updateRequest);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        updateRes.FirstName.Should().Be(updateRequest.FirstName);
        updateRes.LastName.Should().Be(updateRequest.LastName);
        updateRes.IsActive.Should().Be(updateRequest.IsActive);
        updateRes.Roles.Should().Equal(updateRequest.Roles);
    }

    /// <summary>
    /// Verifies that updating a non-existent user returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Update_NonExistent_User()
    {
        await SetAuthTokenAsync();

        var roleService = App.Services.GetRequiredService<IRoleService>();
        var updateFaker = new Faker<UserUpdateRequest>()
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var updateRequest = updateFaker.Generate();
        updateRequest.Roles = [TestRoles.TestRoleId];
        var (updateRsp, _) = await App.Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(updateRequest);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that updating the system-created admin user returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedUserCannotBeUpdated"/>.
    /// </summary>
    [Fact]
    public async Task Update_Admin_User_Should_Fail()
    {
        await SetAuthTokenAsync();

        // Get the system-created user
        var systemCreatedUser = await App.Services.GetRequiredService<IUserService>()
            .Users()
            .FirstAsync(u => u.Username == "admin", cancellationToken: TestContext.Current.CancellationToken);

        var roleService = App.Services.GetRequiredService<IRoleService>();
        var (updateRsp, res) = await App.Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, ProblemDetails>(
            new()
            {
                Id = systemCreatedUser.Id,
                FirstName = "Modified",
                LastName = "Default",
                IsActive = false,
                Roles = [TestRoles.TestRoleId]
            });

        updateRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedUserCannotBeUpdated);
    }
}