namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for password changes: that the new password is what authenticates afterwards, that the old
/// one stops doing so, and that the change is owed to a caller whatever tenant they are acting in
///.
/// </summary>
/// <remarks>
/// A password change does not end the sessions already issued. What a session may do is decided when
/// its token is minted and trusted until that token is replaced, so an access token issued before the
/// change goes on working for the rest of its validity. Revoking a session outright is what signing
/// out is for; this is the trade-off that buys every other request its freedom from a database read.
/// </remarks>
public class ChangePasswordTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that the change takes effect on the credentials rather than on the live session: the
    /// new password authenticates, the old one stops, and the token already issued keeps working.
    /// </summary>
    [Fact]
    public async Task ChangePassword_Replaces_The_Credentials_And_Keeps_The_Session()
    {
        var username = $"password-change-{Guid.NewGuid():N}";
        const string currentPassword = "Current#123";
        const string newPassword = "Changed#123";
        await CreateAdminUserAsync(username, currentPassword);
        await SetAuthTokenAsync(username, currentPassword);

        var (changeResponse, _) = await Client.POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (profileResponse, _) = await Client.GETAsync<ProfileEndpoint, UserProfileResponse>();
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "the token was minted before the change and is trusted until it is replaced");

        await AssertCredentialsReplacedAsync(username, currentPassword, newPassword);
    }

    /// <summary>
    /// Verifies that a caller acting in a tenant can change its own password, which is about the person
    /// rather than about the tenant.
    /// </summary>
    /// <remarks>
    /// The account is made by the test rather than named from the seed, because the password it holds
    /// afterwards is a password this test chose: the suite shares the seeded accounts, and the database
    /// it runs against is not recreated between runs.
    /// </remarks>
    [Fact]
    public async Task Works_For_Any_Caller()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        await SetAuthTokenAsync(account.Username, TestUsers.DefaultPassword);

        const string newPassword = "Changed#123";

        var (response, _) = await Client
            .POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
            {
                CurrentPassword = TestUsers.DefaultPassword,
                NewPassword = newPassword
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the password belongs to the person rather than to the tenant they are working in");

        await AssertCredentialsReplacedAsync(account.Username, TestUsers.DefaultPassword, newPassword);
    }

    /// <summary>
    /// Asserts that the account's password is now the new one and no longer the old one - which is what
    /// says the change was applied rather than merely accepted.
    /// </summary>
    /// <param name="username">The account whose credentials changed.</param>
    /// <param name="oldPassword">The password that must no longer authenticate.</param>
    /// <param name="newPassword">The password that must now authenticate.</param>
    private async Task AssertCredentialsReplacedAsync(string username, string oldPassword, string newPassword)
    {
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (refused, _) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
            {
                Username = username,
                Password = oldPassword
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the old password no longer authenticates");

        var (accepted, _) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
            {
                Username = username,
                Password = newPassword
            });

        accepted.StatusCode.Should().Be(HttpStatusCode.OK, "and the new one does");
    }
}
