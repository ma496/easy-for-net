namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests that signing out ends the session outright: the tenant the session was acting in is recorded
/// on the session's own row rather than kept anywhere the client could hold on to, so discarding the
/// session discards the selection with it, and the next sign-in on the same browser inherits nothing
///.
/// </summary>
/// <remarks>
/// <para>
/// The selection is proved to live on the session rather than in the browser by reading it off the
/// refresh-token row: that row is written when the session is established or switched, it is the only
/// thing a later refresh has to go on, and signing out removes every row the account holds. A client
/// that kept the tenant in a cookie, a tab or local storage would have nothing here to remove, and
/// this case would fail.
/// </para>
/// <para>
/// The account belongs to two tenants, which is what makes the last assertion mean something: after
/// signing out, the next sign-in resolves no tenant by itself, so a session that had inherited the
/// previous selection would report one. The account is built by the test rather than taken from the
/// seed, because signing out revokes every refresh token the account holds, and a seeded account's
/// tokens are shared with whatever else in the suite is signed in as it.
/// </para>
/// </remarks>
public class SignoutTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that signing out discards the session together with the tenant it was acting in, and
    /// that the next sign-in inherits no selection.
    /// </summary>
    [Fact]
    public async Task Signout_Clears_The_Session_Tenant()
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

        // The session selects the tenant it acts in, and reports it - which is the state sign-out has
        // to take away.
        var client = await ClientForAsync(account.Username, acted.Id);

        var (beforeResponse, before) = await client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        before.ActiveTenantId.Should().Be(acted.Id, "the session was established in the tenant it selected");

        // Signing in and then selecting a tenant leaves two rows - the session sign-in established and
        // the one the switch issued - and it is the second that carries the selection. The selection is
        // therefore a property of a session record and not of the client that asked for it.
        (await SessionTenantsAsync(account.Id)).Should().Contain(acted.Id,
            "the tenant the session acts in is recorded on the session's own row, so ending the session is what ends the selection");

        var (signoutResponse, _) = await client.POSTAsync<SignoutEndpoint, EmptyResponse>();

        signoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await SessionTenantsAsync(account.Id)).Should().BeEmpty(
            "signing out discards the session, and with it the tenant the session was acting in");

        // A fresh sign-in on the same browser: the account belongs to both tenants, so nothing decides
        // between them and the caller has to name one. A selection that had survived the sign-out would
        // decide it instead, and the sign-in would be answered without a tenant being named.
        var afterSignout = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (unnamed, refusal) = await afterSignout
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(
                new() { Username = account.Username, Password = TestUsers.DefaultPassword });

        unnamed.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the previous selection was discarded with the session rather than kept for the next one");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantRequired);

        // And the memberships themselves are untouched: naming either tenant signs the caller in.
        await TestsHelper.SetNewAuthTokenAsync(afterSignout, account.Username, TestUsers.DefaultPassword, other.Identifier);

        var (afterResponse, after) = await afterSignout.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        after.ActiveTenantId.Should().Be(other.Id, "the tenant named on the fresh sign-in is the one being acted in");
        after.Tenants.Select(tenant => tenant.Id).Should().BeEquivalentTo([acted.Id, other.Id],
            "the memberships are untouched, so the caller is offered the choice again rather than left without one");
    }

    /// <summary>
    /// The tenants the account's live sessions act in, read off the refresh-token rows - the record a
    /// session writes for itself and the only thing a later refresh has to go on.
    /// </summary>
    /// <param name="userId">The account whose sessions are read.</param>
    /// <returns>The tenant of each session it holds, a <see langword="null"/> entry meaning a session acting in no tenant.</returns>
    private async Task<List<Guid?>> SessionTenantsAsync(Guid userId)
        => await DbContext.AuthTokens
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(token => token.UserId == userId)
            .Select(token => token.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);
}
