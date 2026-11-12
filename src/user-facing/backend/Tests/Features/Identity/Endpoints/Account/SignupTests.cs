using System.Text.Json.Nodes;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Settings.Core;

namespace Backend.Tests.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests what self-service sign-up produces: an account holding the role <c>General.SignupRoleName</c>
/// names, able to sign in at once - and nothing at all while sign-up is closed.
/// </summary>
/// <remarks>
/// The invented accounts here are named from a fresh identifier rather than a counter, because usernames
/// and emails are unique and the test database is neither wiped nor recreated between runs. Whether
/// sign-up is open, and which role it hands out, is set per client through an overlay, never by writing
/// the stored <c>General</c> row every other test reads.
/// </remarks>
public class SignupTests(App app) : UsersTestsBase(app)
{
    private const string SignupPassword = "Signup#123";

    [Fact]
    public async Task Creates_An_Account_Holding_The_Default_User_Role()
    {
        var username = NewUsername();

        var (response, result) = await App.CreateClient(new ClientOptions { HandleCookies = false })
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(Request(username));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "sign-up is open by default");
        result.IsEmailVerificationRequired.Should().BeFalse();

        var account = await DbContext.Users
            .AsNoTracking()
            .Where(user => user.UsernameNormalized == username)
            .Select(user => new { user.IsActive, Roles = user.UserRoles.Select(userRole => userRole.RoleId).ToList() })
            .SingleAsync(TestContext.Current.CancellationToken);

        account.IsActive.Should().BeTrue();
        account.Roles.Should().Equal([TestRoles.UserRoleId], "the default sign-up role is the seeded User role");
    }

    [Fact]
    public async Task The_Signed_Up_Account_Can_Sign_In_And_Holds_No_Permission()
    {
        var username = NewUsername();
        var visitor = SignupVisitorClient();
        (await visitor.POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(Request(username)))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(client, username, SignupPassword);

        var (infoResponse, info) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Username.Should().Be(username);
        info.Roles.Select(role => role.Name).Should().Equal(DataSeeder.UserRoleName);
        info.Roles.SelectMany(role => role.Permissions).Should().BeEmpty("the User role grants nothing");
    }

    [Fact]
    public async Task A_Configured_Role_That_Does_Not_Exist_Creates_The_Account_With_No_Role()
    {
        var username = NewUsername();
        var visitor = AllowSignup(App.CreateClient(new ClientOptions { HandleCookies = false }),
            new JsonObject { ["signupRoleName"] = $"missing-{Guid.NewGuid():N}" });

        (await visitor.POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(Request(username)))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await DbContext.UserRoles
                .AsNoTracking()
                .AnyAsync(userRole => userRole.User.UsernameNormalized == username, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Is_Refused_While_Signup_Is_Disabled()
    {
        var username = NewUsername();
        var visitor = OverlaySetting(App.CreateClient(new ClientOptions { HandleCookies = false }),
            GeneralSettingsProvider.GeneralSettingName, new JsonObject { ["isSignupEnabled"] = false });

        var (response, problem) = await visitor.POSTAsync<SignupEndpoint, SignupRequest, ProblemDetails>(Request(username));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle().Which.Code.Should().Be(ErrorCodes.SignupDisabled.Value);
        (await DbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.UsernameNormalized == username, TestContext.Current.CancellationToken))
            .Should().BeFalse("a closed sign-up creates no account");
    }

    [Fact]
    public async Task Signup_Status_Reports_The_Setting_To_An_Anonymous_Caller()
    {
        var open = await App.CreateClient(new ClientOptions { HandleCookies = false })
            .GETAsync<SignupStatusEndpoint, SignupStatusResponse>();
        var closed = await OverlaySetting(App.CreateClient(new ClientOptions { HandleCookies = false }),
                GeneralSettingsProvider.GeneralSettingName, new JsonObject { ["isSignupEnabled"] = false })
            .GETAsync<SignupStatusEndpoint, SignupStatusResponse>();

        open.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        open.Result.IsSignupEnabled.Should().BeTrue("sign-up is open by default");
        closed.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        closed.Result.IsSignupEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Refuses_A_Username_Already_In_Use()
    {
        var (response, problem) = await SignupVisitorClient()
            .POSTAsync<SignupEndpoint, SignupRequest, ProblemDetails>(Request(TestUsers.AdminUsername, $"{Guid.NewGuid():N}@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.UsernameAlreadyExists.Value);
    }

    [Fact]
    public async Task Requires_Verification_When_The_Setting_Says_So_And_Mails_The_Link()
    {
        var username = NewUsername();
        var visitor = AllowSignup(App.CreateClient(new ClientOptions { HandleCookies = false }),
            new JsonObject { ["isEmailVerificationRequired"] = true });

        var (response, result) = await visitor.POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(Request(username));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsEmailVerificationRequired.Should().BeTrue();
    }

    private static string NewUsername() => $"signup-{Guid.NewGuid():N}";

    private static SignupRequest Request(string username, string? email = null) => new()
    {
        Username = username,
        Email = email ?? $"{username}@example.com",
        Password = SignupPassword,
        ConfirmPassword = SignupPassword
    };
}
