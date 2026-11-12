using System.Text.Json.Nodes;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Settings.Core;
using Backend.Tests.Fakes;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Settings;

/// <summary>
/// Tests for the <c>General</c> setting as sign-in and refresh read it: requiring a verified email refuses
/// an unverified account, admits a verified one, and reaches a live session at its next renewal without
/// revoking it.
/// </summary>
/// <remarks>
/// The requirement is applied through <see cref="SettingOverlays"/> to one client's requests, never
/// written as a stored row - see <see cref="SettingsTestsBase"/>.
/// </remarks>
public class GeneralSettingsTests(App app) : SettingsTestsBase(app)
{
    private const string RefreshRoute = "api/account/refresh-token";

    private static JsonObject VerificationRequired() => new() { ["isEmailVerificationRequired"] = true };

    [Fact]
    public async Task Requirement_Refuses_An_Unverified_Account()
    {
        var account = await CreateUserAsync();

        var (response, problem) = await SignInAsync<ProblemDetails>(account.Username, VerificationRequired());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    [Fact]
    public async Task Requirement_Admits_A_Verified_Account()
    {
        var account = await CreateUserAsync();
        await MarkEmailVerifiedAsync(account);

        var (response, _) = await SignInAsync<TokenResponse>(account.Username, VerificationRequired());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Requirement_Set_After_Sign_In_Refuses_The_Next_Refresh_And_Revokes_Nothing()
    {
        var account = await CreateUserAsync();
        var session = await SessionForAsync(account.Username);

        var anonymous = OverlaySetting(App.CreateClient(new ClientOptions { HandleCookies = false }),
            GeneralSettingsProvider.GeneralSettingName, VerificationRequired());

        (await session.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>()).Response.StatusCode
            .Should().Be(HttpStatusCode.OK, "a settings change revokes no session");

        var (response, refusal) = await anonymous.POSTAsync<FastEndpoints.Security.TokenRequest, ProblemDetails>(
            RefreshRoute,
            new() { UserId = session.UserId.ToString(), RefreshToken = session.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the setting standing at refresh decides");
        refusal.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.EmailNotVerified.Value);
    }

    /// <summary>Signs an account in on an anonymous client whose requests see <paramref name="general"/>.</summary>
    private async Task<TestResult<TResponse>> SignInAsync<TResponse>(string username, JsonObject general)
    {
        var anonymous = OverlaySetting(App.CreateClient(new ClientOptions { HandleCookies = false }),
            GeneralSettingsProvider.GeneralSettingName, general);
        return await anonymous.POSTAsync<TokenEndpoint, TokenRequest, TResponse>(new()
        {
            Username = username,
            Password = TestUsers.DefaultPassword
        });
    }
}
