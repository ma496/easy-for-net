namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for password changes: that the new password is what authenticates afterwards, that the old
/// one stops doing so, that every other session of the account ends while the one making the change
/// survives, and that the change is owed to a caller whatever tenant they are acting in.
/// </summary>
/// <remarks>
/// A password change ends every other session of the account - its access token answers 401 and its
/// refresh token is refused - so whoever held the old password is locked out. The session making the
/// change is kept, so the person changing the password is not signed out by doing so.
/// </remarks>
public class ChangePasswordTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies that the change replaces the credentials and ends the account's other sessions: the new
    /// password authenticates, the old one stops, the session that made the change keeps working, and a
    /// second session of the same account answers 401 without renewal and cannot be refreshed.
    /// </summary>
    [Fact]
    public async Task ChangePassword_Replaces_The_Credentials_And_Keeps_The_Session()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var current = await SessionForAsync(account.Username, tenant.Id);
        var other = await SessionForAsync(account.Username, tenant.Id);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id);
        const string newPassword = "Changed#123";

        var (changeResponse, _) = await current.Client.POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
        {
            CurrentPassword = TestUsers.DefaultPassword,
            NewPassword = newPassword
        });
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (profileResponse, _) = await current.Client.GETAsync<ProfileEndpoint, UserProfileResponse>();
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "the session that made the change is kept");
        await AssertAliveAsync(current);
        await AssertEndedAsync(other);
        await AssertAliveAsync(bystander);

        await AssertCredentialsReplacedAsync(account.Username, TestUsers.DefaultPassword, newPassword);
    }

    /// <summary>
    /// Verifies a change refused for a wrong current password ends no session.
    /// </summary>
    [Fact]
    public async Task A_Refused_Change_Leaves_Every_Session_Working()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var current = await SessionForAsync(account.Username, tenant.Id);
        var other = await SessionForAsync(account.Username, tenant.Id);

        var (response, _) = await current.Client.POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, ProblemDetails>(new()
        {
            CurrentPassword = "Wrong#1234",
            NewPassword = "Changed#123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertAliveAsync(current);
        await AssertAliveAsync(other);
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
