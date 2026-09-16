namespace Backend.Tests.Features.Identity.Core;

using System.Text.Json;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tenancy;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for <see cref="IAuthTokenService"/> and <see cref="IAuthTokenCleanService"/> covering token validation, saving, and expiration cleanup, and for the tenant a refresh carries forward (AC-109).
/// </summary>
public class AuthTokenServiceTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The route the refresh-token service registers, addressed directly because a refresh is taken
    /// with the refresh token as its own credential rather than through a signed-in client.
    /// </summary>
    private const string RefreshRoute = "api/account/refresh-token";

    /// <summary>
    /// Verifies that refreshing a session re-establishes the very tenant it already had: the renewed
    /// access token names that tenant and never none, and never the one the caller acted in before
    /// selecting, while a session that had selected none stays without one (AC-109).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A refresh has nothing but the refresh-token row to go on - the access token it renews has
    /// expired by then - so the tenant is read off that row and written forward onto the one issued in
    /// its place. Three sessions are established for one account, acting in the first tenant, in the
    /// second and in neither, so that a tenant which had merely stopped changing would be caught: each
    /// has to come back naming its own.
    /// </para>
    /// <para>
    /// The account holds two memberships, which is what leaves sign-in resolving no tenant for it: a
    /// session acts in a tenant only once one has been selected, so the sessions that act in the first
    /// and second tenants are established by switching into them. That also means this account's
    /// sessions are its own - a seeded account's would be shared with whatever else in the suite is
    /// signed in as it, and refreshing one consumes the row it was read from.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Refresh_Preserves_The_Active_Tenant()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();

        var account = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_View));

        await MembershipService.AddAsync(
            other.Id,
            account.Id,
            [await CreateTenantRoleAsync(other.Id, Allow.User_View)],
            TestContext.Current.CancellationToken);

        var inActed = await EstablishSessionAsync(account.Username, acted.Id);
        var inOther = await EstablishSessionAsync(account.Username, other.Id);
        var inNone = await EstablishSessionAsync(account.Username);

        await AssertRefreshCarriesAsync(account.Id, inActed, acted.Id);
        await AssertRefreshCarriesAsync(account.Id, inOther, other.Id);
        await AssertRefreshCarriesAsync(account.Id, inNone, expectedTenantId: null);
    }

    /// <summary>
    /// Refreshes one session and verifies that the tenant it was established in comes back unchanged.
    /// </summary>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="refreshToken">The refresh token the session was issued.</param>
    /// <param name="expectedTenantId">The tenant that session acts in, or <see langword="null"/> for none.</param>
    private async Task AssertRefreshCarriesAsync(Guid userId, string refreshToken, Guid? expectedTenantId)
    {
        var before = await SessionTenantsAsync(userId);

        // No bearer token: the refresh token is the credential, so this request is anonymous and the
        // tenant comes from the row alone - never from anything the caller asserts for itself.
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, refreshed) = await anonymous
            .POSTAsync<FastEndpoints.Security.TokenRequest, TokenResponse>(
                RefreshRoute,
                new() { UserId = userId.ToString(), RefreshToken = refreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        TenantClaimOf(refreshed.AccessToken).Should().Be(expectedTenantId,
            "a refresh re-establishes the tenant the session already had - it neither loses it as the session is renewed nor resurrects the one the caller acted in before selecting");

        // The pair is renewed rather than replayed: the row the token was read from is spent and one
        // carrying the same tenant takes its place. Written forward rather than re-derived, and never
        // moved to another tenant - either would show here as a different set of sessions.
        (await SessionTenantsAsync(userId)).Should().Equal(before,
            "the session that was refreshed is replaced by one acting in the same tenant, so the selection survives the renewal itself and not merely the response that reported it");
    }

    /// <summary>
    /// Establishes a session for an account, in the tenant named or in none, and returns the refresh
    /// token that session was issued - the credential a refresh is taken with.
    /// </summary>
    /// <param name="username">The account to sign in as.</param>
    /// <param name="tenantId">The tenant to select, or <see langword="null"/> to select none.</param>
    /// <returns>The refresh token the session was issued.</returns>
    private async Task<string> EstablishSessionAsync(string username, Guid? tenantId = null)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (_, signedIn) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = username, Password = TestUsers.DefaultPassword });

        if (tenantId is not { } selected)
        {
            return signedIn.RefreshToken;
        }

        // Switching is an authenticated call, so the token sign-in issued is what authorises it. The
        // re-established session is the one whose refresh token is returned, not the sign-in's.
        TestsHelper.SetAuthToken(client, signedIn.AccessToken);

        var (_, switched) = await client
            .POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(
                new() { TenantId = selected });

        return switched.Session.RefreshToken;
    }

    /// <summary>
    /// The tenant each session of an account acts in, read off the refresh-token rows - the record a
    /// session writes for itself and the only thing a later refresh has to go on. A
    /// <see langword="null"/> entry is a session acting in no tenant.
    /// </summary>
    /// <param name="userId">The account whose sessions are read.</param>
    /// <returns>One entry per session the account holds.</returns>
    private async Task<List<Guid?>> SessionTenantsAsync(Guid userId)
        => await DbContext.AuthTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .OrderBy(token => token.TenantId)
            .Select(token => token.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The tenant an access token names, read out of the token itself so that what is asserted is what
    /// the session was issued with rather than what a later request made of it.
    /// </summary>
    /// <param name="accessToken">The token to read.</param>
    /// <returns>The tenant it names, or <see langword="null"/> when it names none.</returns>
    private static Guid? TenantClaimOf(string accessToken)
    {
        var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using var document = JsonDocument.Parse(Convert.FromBase64String(payload));

        return document.RootElement.TryGetProperty(ClaimConstants.TenantId, out var claim)
               && Guid.TryParse(claim.GetString(), out var tenantId)
            ? tenantId
            : null;
    }

    /// <summary>
    /// Verifies that a valid refresh token can be consumed exactly once.
    /// </summary>
    [Fact]
    public async Task ConsumeRefreshTokenAsync_ShouldReturnTrueOnce_WhenTokenIsValid()
    {
        var authTokenService = App.Services.GetRequiredService<IAuthTokenService>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var token = NewToken(TestUsers.TestUserId, $"{Guid.NewGuid()}_{Faker.GlobalUniqueIndex}", DateTime.UtcNow.AddDays(1), $"{Guid.NewGuid()}_{Faker.GlobalUniqueIndex}", DateTime.UtcNow.AddDays(1));
        await authTokenService.SaveTokenAsync(token, tenantId: null);
        var request = new FastEndpoints.Security.TokenRequest { RefreshToken = token.RefreshToken, UserId = TestUsers.TestUserId.ToString() };
        var consumption = await authTokenService.ConsumeRefreshTokenAsync(request, cancellationToken);
        var replayConsumption = await authTokenService.ConsumeRefreshTokenAsync(request, cancellationToken);

        consumption.Consumed.Should().BeTrue();
        consumption.TenantId.Should().BeNull();
        replayConsumption.Consumed.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that an expired refresh token cannot be consumed.
    /// </summary>
    [Fact]
    public async Task ConsumeRefreshTokenAsync_ShouldReturnFalse_WhenTokenIsInvalid()
    {
        var authTokenService = App.Services.GetRequiredService<IAuthTokenService>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var token = NewToken(TestUsers.TestUserId, $"{Guid.NewGuid()}_{Faker.GlobalUniqueIndex}", DateTime.UtcNow.AddDays(-1), $"{Guid.NewGuid()}_{Faker.GlobalUniqueIndex}", DateTime.UtcNow.AddDays(-1));
        await authTokenService.SaveTokenAsync(token, tenantId: null);
        var consumption = await authTokenService.ConsumeRefreshTokenAsync(
            new FastEndpoints.Security.TokenRequest { RefreshToken = token.RefreshToken, UserId = TestUsers.TestUserId.ToString() }, cancellationToken);

        consumption.Consumed.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that <see cref="IAuthTokenCleanService.DeleteExpiredTokensAsync"/> removes expired tokens from the database.
    /// </summary>
    [Fact]
    public async Task DeleteExpiredTokensAsync_ShouldDeleteExpiredTokens()
    {
        var authTokenService = App.Services.GetRequiredService<IAuthTokenService>();
        var authTokenCleanService = App.Services.GetRequiredService<IAuthTokenCleanService>();
        // create expired token
        var expiredToken = NewToken(TestUsers.TestUserId, $"{Guid.NewGuid()}_{Faker.GlobalUniqueIndex}", DateTime.UtcNow.AddDays(-1), $"{Guid.NewGuid()}_{Faker.GlobalUniqueIndex}", DateTime.UtcNow.AddDays(-1));
        var token = await authTokenService.SaveTokenAsync(expiredToken, tenantId: null);
        // delete expired tokens
        await authTokenCleanService.DeleteExpiredTokensAsync();

        // assert
        var deletedToken = await DbContext.AuthTokens
            .Where(t => t.Id == token.Id)
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);

        deletedToken.Should().BeNull();
    }

    /// <summary>
    /// Creates a <see cref="TokenResponse"/> with specified expiry dates using reflection to set private properties.
    /// </summary>
    private static TokenResponse NewToken(Guid userId, string accessToken, DateTime accessExpiry, string refreshToken, DateTime refreshExpiry)
    {
        var expiredToken = new TokenResponse
        {
            UserId = userId.ToString(),
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
        // set expiry to 1 day ago, use reflection to access private property
        var accessExpiryProperty = expiredToken.GetType().GetProperty("AccessExpiry");
        accessExpiryProperty?.SetValue(expiredToken, accessExpiry);
        var refreshExpiryProperty = expiredToken.GetType().GetProperty("RefreshExpiry");
        refreshExpiryProperty?.SetValue(expiredToken, refreshExpiry);
        return expiredToken;
    }
}
