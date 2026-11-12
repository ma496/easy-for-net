using Backend.Features.Identity.Endpoints.Account;

namespace Backend.Tests.Features.Identity.Sessions;

/// <summary>
/// Tests for the account security stamp that ends a refresh chain across a credential change even when
/// its refresh-token row escaped the revocation - the row a renewal racing a password reset writes after
/// the reset has already swept the account.
/// </summary>
/// <remarks>
/// The race itself cannot be interleaved from a test, so its outcome is arranged directly: the account's
/// stamp is rotated while a row issued under the old one is left in place, which is exactly the state a
/// renewal that lost the race leaves behind.
/// </remarks>
public class SecurityStampTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies a refresh token whose row was issued under a stamp the account no longer holds is refused,
    /// while the chain of another account is untouched.
    /// </summary>
    [Fact]
    public async Task Refresh_Issued_Under_A_Replaced_Stamp_Is_Refused()
    {
        var account = await CreateUserAsync();
        var stale = await SessionForAsync(account.Username);
        var bystander = await SessionForAsync((await CreateUserAsync()).Username);

        await DbContext.Users
            .Where(user => user.Id == account.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()),
                TestContext.Current.CancellationToken);

        (await DbContext.AuthTokens.AsNoTracking()
                .AnyAsync(row => row.UserId == account.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue("the row escaped revocation, which is the case the stamp exists for");
        (await RefreshStatusAsync(stale)).Should().Be(HttpStatusCode.Unauthorized,
            "the row was issued under credentials the account no longer holds");
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies a fresh sign-in after the stamp moved on issues a chain that renews, so rotating the stamp
    /// ends old chains without locking the account out.
    /// </summary>
    [Fact]
    public async Task Sign_In_After_Rotation_Renews()
    {
        var account = await CreateUserAsync();

        await DbContext.Users
            .Where(user => user.Id == account.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()),
                TestContext.Current.CancellationToken);

        var session = await SessionForAsync(account.Username);

        await AssertAliveAsync(session);
    }

    private static async Task ChangeOwnPasswordAsync(HttpClient client)
    {
        var (response, _) = await client.POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
        {
            CurrentPassword = TestUsers.DefaultPassword,
            NewPassword = "Changed#123"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpStatusCode> RenewalStatusAsync(Guid userId, string refreshToken)
    {
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (response, _) = await anonymous.POSTAsync<FastEndpoints.Security.TokenRequest, TokenResponse>(
            "api/account/refresh-token",
            new() { UserId = userId.ToString(), RefreshToken = refreshToken });
        return response.StatusCode;
    }

    private async Task<List<Guid>> RowIdsAsync(Guid userId)
        => await DbContext.AuthTokens.AsNoTracking()
            .Where(row => row.UserId == userId)
            .Select(row => row.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
}
