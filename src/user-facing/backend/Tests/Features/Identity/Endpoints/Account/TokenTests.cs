using System.Text.Json.Nodes;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Settings.Core;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests sign-in: by username or by email, the refusals of wrong credentials, of a deactivated account
/// and of an unverified one while verification is required, and that a live session of an account that
/// is deactivated is refused at its next renewal.
/// </summary>
/// <remarks>
/// A refusal of the credentials is answered as a bad request carrying its code, never 401: a 401 would
/// tell the web client to go and renew a session the caller was never given.
/// </remarks>
public class TokenTests(App app) : UsersTestsBase(app)
{
    [Fact]
    public async Task Signs_In_By_Username()
    {
        var account = await CreateUserAsync();

        (await AuthenticateAsync(new() { Username = account.Username, Password = TestUsers.DefaultPassword }))
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signs_In_By_Email()
    {
        var account = await CreateUserAsync();

        (await AuthenticateAsync(new() { IsEmail = true, Email = account.Email, Password = TestUsers.DefaultPassword }))
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_Password_Is_Refused()
    {
        var account = await CreateUserAsync();

        var (response, problem) = await AnonymousClient().POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
            new() { Username = account.Username, Password = "Wrong#1234" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.InvalidUsernamePassword.Value);
    }

    [Fact]
    public async Task Unknown_Email_Is_Refused()
    {
        var (response, problem) = await AnonymousClient().POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
            new() { IsEmail = true, Email = $"{Guid.NewGuid():N}@example.com", Password = TestUsers.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.InvalidEmailPassword.Value);
    }

    [Fact]
    public async Task Deactivated_Account_Cannot_Authenticate_And_Reactivated_One_Can()
    {
        var account = await CreateUserAsync();
        await SetActiveAsync(account.Id, isActive: false);

        var (refused, refusal) = await AnonymousClient().POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
            new() { Username = account.Username, Password = TestUsers.DefaultPassword });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.UserNotActive.Value);

        await SetActiveAsync(account.Id, isActive: true);

        (await AuthenticateAsync(new() { Username = account.Username, Password = TestUsers.DefaultPassword }))
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivating_An_Account_Refuses_The_Next_Renewal_Of_A_Live_Session()
    {
        var account = await CreateUserAsync();
        var session = await SessionForAsync(account.Username);
        await SetActiveAsync(account.Id, isActive: false);

        var (renewal, renewalRefusal) = await AnonymousClient()
            .POSTAsync<FastEndpoints.Security.TokenRequest, ProblemDetails>(
                "api/account/refresh-token",
                new() { UserId = account.Id.ToString(), RefreshToken = session.RefreshToken });

        renewal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        renewalRefusal.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.UserNotActive.Value);
    }

    [Fact]
    public async Task Unverified_Account_Is_Refused_While_Verification_Is_Required()
    {
        var account = await CreateUserAsync();
        var client = OverlaySetting(AnonymousClient(), GeneralSettingsProvider.GeneralSettingName,
            new JsonObject { ["isEmailVerificationRequired"] = true });

        var (response, problem) = await client.POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
            new() { Username = account.Username, Password = TestUsers.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    [Fact]
    public async Task The_Token_Names_Only_The_Account_And_The_Session()
    {
        var account = await CreateUserAsync();

        var (_, token) = await AnonymousClient().POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = account.Username, Password = TestUsers.DefaultPassword });

        var payload = TestsHelper.PayloadOf(token.AccessToken);
        payload.Keys.Should().NotContain(["permission", "role", "tenant_id", "is_platform"]);
        (await SessionOfAsync(token.AccessToken))!.UserId.Should().Be(account.Id);
    }

    private HttpClient AnonymousClient() => App.CreateClient(new ClientOptions { HandleCookies = false });

    private async Task<HttpStatusCode> AuthenticateAsync(TokenRequest request)
        => (await AnonymousClient().POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(request)).Response.StatusCode;

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        var account = await DbContext.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
        account.IsActive = isActive;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
