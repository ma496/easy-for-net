using Backend.Features.Identity.Endpoints.Users;

namespace Backend.Tests.Features.Identity.Endpoints.Users;

/// <summary>
/// Tests that <see cref="UserUpdateEndpoint"/> and <see cref="UserDeleteEndpoint"/> end the live sessions
/// their change invalidates - deactivating or deleting an account, or changing its roles, ends all of
/// them - and leave every other session, and every refused change, alone.
/// </summary>
/// <remarks>
/// Every role and account is made by the test, and the administrator is an account of its own holding
/// just the permissions the call needs, so nothing seeded is written and the tests run beside the rest of
/// the suite.
/// </remarks>
public class UserSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    [Fact]
    public async Task Deactivating_A_User_Ends_All_Its_Sessions()
    {
        var (admin, roleId, _) = await ArrangeAsync();
        var target = await CreateUserAsync(roleId);
        var first = await SessionForAsync(target.Username);
        var second = await SessionForAsync(target.Username);
        var bystander = await SessionForAsync((await CreateUserAsync(roleId)).Username);

        var (response, _) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = target.Id, IsActive = false, Roles = [roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    [Fact]
    public async Task Deleting_A_User_Ends_All_Its_Sessions()
    {
        var (admin, roleId, _) = await ArrangeAsync();
        var target = await CreateUserAsync(roleId);
        var first = await SessionForAsync(target.Username);
        var second = await SessionForAsync(target.Username);
        var bystander = await SessionForAsync((await CreateUserAsync(roleId)).Username);

        var (response, _) = await admin.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(
            new() { Id = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    [Fact]
    public async Task Changing_A_Users_Roles_Ends_Its_Sessions()
    {
        var (admin, roleId, otherRoleId) = await ArrangeAsync();
        var target = await CreateUserAsync(roleId);
        var first = await SessionForAsync(target.Username);
        var second = await SessionForAsync(target.Username);
        var bystander = await SessionForAsync((await CreateUserAsync(roleId)).Username);

        var (response, _) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = target.Id, IsActive = true, Roles = [otherRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    [Fact]
    public async Task Updating_Only_The_Name_Revokes_Nothing()
    {
        var (admin, roleId, _) = await ArrangeAsync();
        var target = await CreateUserAsync(roleId);
        var session = await SessionForAsync(target.Username);

        var (response, _) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = target.Id, IsActive = true, FirstName = "Renamed", LastName = "Person", Roles = [roleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertAliveAsync(session);
    }

    [Fact]
    public async Task A_Refused_Update_Leaves_Sessions_Working()
    {
        var (admin, roleId, _) = await ArrangeAsync();
        var target = await CreateUserAsync(roleId);
        var first = await SessionForAsync(target.Username);
        var second = await SessionForAsync(target.Username);

        var (response, problem) = await admin.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, ProblemDetails>(
            new() { Id = target.Id, IsActive = false, Roles = [Guid.NewGuid()] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.ReferencedRecordNotFound.Value);
        await AssertAliveAsync(first);
        await AssertAliveAsync(second);
    }

    /// <summary>
    /// An administrator holding just the user permissions, a role for the accounts the test acts on, and
    /// a second role to move them to.
    /// </summary>
    private async Task<(HttpClient Admin, Guid RoleId, Guid OtherRoleId)> ArrangeAsync()
    {
        var adminRoleId = await CreateRoleAsync(Allow.User_Update, Allow.User_Delete, Allow.User_View);
        var administrator = await CreateUserAsync(adminRoleId);
        var admin = await ClientForAsync(administrator.Username);
        var roleId = await CreateRoleAsync(Allow.User_View);
        var otherRoleId = await CreateRoleAsync(Allow.Role_View);

        return (admin, roleId, otherRoleId);
    }
}
