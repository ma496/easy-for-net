using Backend.Features.Identity.Endpoints.Account;

namespace Backend.Tests.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests that the identity call reports who the caller is and what they may do: the account's profile
/// fields and the roles the session holds, each with the permissions it grants.
/// </summary>
public class GetInfoTests(App app) : UsersTestsBase(app)
{
    [Fact]
    public async Task Reports_The_Callers_Profile_Roles_And_Permissions()
    {
        var roleId = await CreateRoleAsync(Allow.User_View, Allow.Seo_View);
        var account = await CreateUserAsync(roleId);
        await SignInAsAsync(account.Username);

        var (response, info) = await Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Id.Should().Be(account.Id);
        info.Username.Should().Be(account.Username);
        info.Email.Should().Be(account.Email);
        var role = info.Roles.Should().ContainSingle().Subject;
        role.Id.Should().Be(roleId);
        role.Permissions.Select(permission => permission.Name).Should().BeEquivalentTo([Allow.User_View, Allow.Seo_View]);
    }

    [Fact]
    public async Task The_Administrator_Holds_Every_Permission()
    {
        await SetAuthTokenAsync();

        var (_, info) = await Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        info.Roles.Select(role => role.Name).Should().Contain(DataSeeder.AdminRoleName);
        info.Roles.SelectMany(role => role.Permissions).Select(permission => permission.Name)
            .Should().Contain([Allow.Admin_Access, Allow.Seo_Update, Allow.Hangfire_Dashboard]);
    }

    [Fact]
    public async Task Anonymous_Caller_Is_Unauthorized()
    {
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, _) = await anonymous.GETAsync<GetInfoEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
