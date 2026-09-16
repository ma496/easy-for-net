namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for password changes and session invalidation, including the caller who belongs to no tenant
/// at all - changing one's own password is account self-service and is owed to them too (AC-051).
/// </summary>
public class ChangePasswordTests(App app) : TenancyTestsBase(app)
{
    [Fact]
    public async Task ChangePassword_InvalidatesExistingAccessToken()
    {
        var username = $"password-change-{Guid.NewGuid():N}";
        const string currentPassword = "Current#123";
        const string newPassword = "Changed#123";
        await CreateAdminUserAsync(username, currentPassword);
        await SetAuthTokenAsync(username, currentPassword);

        var (changeResponse, _) = await App.Client.POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (profileResponse, _) = await App.Client.GETAsync<ProfileEndpoint, UserProfileResponse>();
        profileResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that an account holding no membership in any active tenant can still change its own
    /// password, rather than being refused for want of a tenant (AC-051).
    /// </summary>
    /// <remarks>
    /// The account is made by the test rather than named from the seed, because the password it holds
    /// afterwards is a password this test chose: the suite shares the seeded accounts, and the database
    /// it runs against is not recreated between runs.
    /// </remarks>
    [Fact]
    public async Task Works_Without_An_Active_Tenant()
    {
        var account = await CreateAccountWithoutMembershipAsync();
        await SetAuthTokenAsync(account.Username, TestUsers.DefaultPassword);

        var (response, _) = await App.Client
            .POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
            {
                CurrentPassword = TestUsers.DefaultPassword,
                NewPassword = "Changed#123"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the password belongs to the person, not to a tenant, so a caller acting in none is still owed this");

        // The change took effect: the credentials it was made with are no longer the account's.
        var (refused, _) = await App.Client
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
            {
                Username = account.Username,
                Password = TestUsers.DefaultPassword
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the old password no longer authenticates, which is what says the change was applied rather than merely accepted");
    }
}
