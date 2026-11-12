using Backend.Features.Identity.Endpoints.Roles;

namespace Backend.Tests.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests that <see cref="ChangePermissionsEndpoint"/> and <see cref="RoleDeleteEndpoint"/> end the sessions
/// of the accounts holding the role and nobody else's, and that <see cref="RoleUpdateEndpoint"/>, which
/// grants nothing, ends none.
/// </summary>
public class RoleSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    [Fact]
    public async Task Changing_A_Roles_Permissions_Ends_The_Holders_Sessions()
    {
        var arrangement = await ArrangeAsync(Allow.Role_ChangePermissions);

        var (response, _) = await arrangement.Admin.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new() { Id = arrangement.RoleId, Permissions = [await PermissionIdAsync(Allow.Role_View), await PermissionIdAsync(Allow.User_View)] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(arrangement.HolderFirst);
        await AssertEndedAsync(arrangement.HolderSecond);
        await AssertAliveAsync(arrangement.OtherRoleUser);
    }

    [Fact]
    public async Task Deleting_A_Role_Ends_The_Holders_Sessions()
    {
        var arrangement = await ArrangeAsync(Allow.Role_Delete);

        var (response, _) = await arrangement.Admin.DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(
            new() { Id = arrangement.RoleId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(arrangement.HolderFirst);
        await AssertEndedAsync(arrangement.HolderSecond);
        await AssertAliveAsync(arrangement.OtherRoleUser);
    }

    [Fact]
    public async Task Resubmitting_The_Same_Permissions_Revokes_Nothing()
    {
        var arrangement = await ArrangeAsync(Allow.Role_ChangePermissions);

        var (response, _) = await arrangement.Admin.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new() { Id = arrangement.RoleId, Permissions = [await PermissionIdAsync(Allow.Role_View)] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertAliveAsync(arrangement.HolderFirst);
        await AssertAliveAsync(arrangement.HolderSecond);
    }

    [Fact]
    public async Task A_Refused_Permission_Change_Leaves_Sessions_Working()
    {
        var arrangement = await ArrangeAsync(Allow.Role_ChangePermissions);

        var (response, _) = await arrangement.Admin.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ProblemDetails>(
            new() { Id = Guid.NewGuid(), Permissions = [await PermissionIdAsync(Allow.User_View)] });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertAliveAsync(arrangement.HolderFirst);
        await AssertAliveAsync(arrangement.HolderSecond);
    }

    [Fact]
    public async Task Renaming_A_Role_Revokes_Nothing()
    {
        var arrangement = await ArrangeAsync(Allow.Role_Update);

        var (response, _) = await arrangement.Admin.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(
            new() { Id = arrangement.RoleId, Name = $"Renamed {Guid.NewGuid():N}", Description = "Renamed by a test" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertAliveAsync(arrangement.HolderFirst);
        await AssertAliveAsync(arrangement.HolderSecond);
        await AssertAliveAsync(arrangement.OtherRoleUser);
    }

    /// <summary>
    /// What a test starts from: an administrator, a role with two sessions of its holder, and a session
    /// of an account holding another role.
    /// </summary>
    private sealed record Arrangement(
        HttpClient Admin,
        Guid RoleId,
        RenewableSession HolderFirst,
        RenewableSession HolderSecond,
        RenewableSession OtherRoleUser);

    /// <param name="administratorPermission">The one permission the administrator holds.</param>
    private async Task<Arrangement> ArrangeAsync(string administratorPermission)
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(administratorPermission));
        var admin = await ClientForAsync(administrator.Username);

        var roleId = await CreateRoleAsync(Allow.Role_View);
        var otherRoleId = await CreateRoleAsync(Allow.User_View);

        var holder = await CreateUserAsync(roleId);
        var otherRoleUser = await CreateUserAsync(otherRoleId);

        return new Arrangement(
            admin,
            roleId,
            await SessionForAsync(holder.Username),
            await SessionForAsync(holder.Username),
            await SessionForAsync(otherRoleUser.Username));
    }
}
