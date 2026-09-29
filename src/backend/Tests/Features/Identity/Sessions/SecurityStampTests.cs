namespace Backend.Tests.Features.Identity.Sessions;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;

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
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var stale = await SessionForAsync(account.Username, tenant.Id);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id);

        await DbContext.Users
            .Where(user => user.Id == account.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()),
                TestContext.Current.CancellationToken);

        (await DbContext.AuthTokens.AsNoTracking().AcrossAllTenants()
                .AnyAsync(row => row.UserId == account.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue("the row escaped revocation, which is the case the stamp exists for");
        (await RefreshStatusAsync(stale)).Should().Be(HttpStatusCode.Unauthorized,
            "the row was issued under credentials the account no longer holds");
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies a session minted before the stamp moved on cannot be traded, through a tenant switch, for a
    /// session and a refresh token issued under the new stamp: the switch is refused and leaves no row.
    /// </summary>
    [Fact]
    public async Task Switch_From_A_Session_Minted_Under_A_Replaced_Stamp_Is_Refused()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var account = await CreateDualTenantMemberAsync(first.Id, second.Id);
        var stale = await SessionForAsync(account.Username, first.Id);

        await DbContext.Users
            .Where(user => user.Id == account.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()),
                TestContext.Current.CancellationToken);
        var rowsBefore = await RowIdsAsync(account.Id);

        var (response, _) = await stale.Client.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, ProblemDetails>(
            new() { TenantId = second.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the session authorizing the switch was minted under credentials the account no longer holds");
        (await RowIdsAsync(account.Id)).Should().BeSubsetOf(rowsBefore,
            "the pair the refused switch issued is withdrawn, so nothing new can renew");
    }

    /// <summary>
    /// Verifies a switch from a session minted under the account's current stamp still works, and its
    /// refresh token renews.
    /// </summary>
    [Fact]
    public async Task Switch_Under_The_Current_Stamp_Renews()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var account = await CreateDualTenantMemberAsync(first.Id, second.Id);
        await SignInAsAsync(account.Username, first.Id);

        var (response, switched) = await Client.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(
            new() { TenantId = second.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });
        var (renewal, _) = await anonymous.POSTAsync<FastEndpoints.Security.TokenRequest, TokenResponse>(
            "api/account/refresh-token",
            new() { UserId = account.Id.ToString(), RefreshToken = switched.Session.RefreshToken });
        renewal.StatusCode.Should().Be(HttpStatusCode.OK, "the switch carried the stamp its session was minted under");
    }

    /// <summary>
    /// Verifies the session a caller changed its own password from is moved onto the new stamp whole, so it
    /// can still switch tenant, and the refresh token the switch issued renews.
    /// </summary>
    [Fact]
    public async Task Switch_After_Changing_Ones_Own_Password_Works()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var account = await CreateDualTenantMemberAsync(first.Id, second.Id);
        var session = await SessionForAsync(account.Username, first.Id);
        await ChangeOwnPasswordAsync(session.Client);

        var (response, switched) = await session.Client.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(
            new() { TenantId = second.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the kept session was moved onto the new stamp, record and row alike");
        (await RenewalStatusAsync(account.Id, switched.Session.RefreshToken)).Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies a platform account that changed its password from inside a tenant can still exit to
    /// platform scope, and the refresh token the exit issued renews.
    /// </summary>
    [Fact]
    public async Task Exit_After_Changing_Ones_Own_Password_Works()
    {
        var tenant = await CreateTenantAsync();
        var account = await SignInAsPlatformAdministratorEnteringAsync(tenant.Id);
        await ChangeOwnPasswordAsync(Client);

        var (response, exited) = await Client.POSTAsync<TenantExitEndpoint, TenantExitResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the kept session was moved onto the new stamp, record and row alike");
        (await RenewalStatusAsync(account.Id, exited.Session.RefreshToken)).Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies a fresh sign-in after the stamp moved on issues a chain that renews, so rotating the stamp
    /// ends old chains without locking the account out.
    /// </summary>
    [Fact]
    public async Task Sign_In_After_Rotation_Renews()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);

        await DbContext.Users
            .Where(user => user.Id == account.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityStamp, Guid.NewGuid()),
                TestContext.Current.CancellationToken);

        var session = await SessionForAsync(account.Username, tenant.Id);

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
        => await DbContext.AuthTokens.AsNoTracking().AcrossAllTenants()
            .Where(row => row.UserId == userId)
            .Select(row => row.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
}
