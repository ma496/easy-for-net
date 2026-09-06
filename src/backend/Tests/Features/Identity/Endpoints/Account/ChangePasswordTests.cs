namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests for password changes and session invalidation.
/// </summary>
public class ChangePasswordTests(App app) : AppTestsBase(app)
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
}
