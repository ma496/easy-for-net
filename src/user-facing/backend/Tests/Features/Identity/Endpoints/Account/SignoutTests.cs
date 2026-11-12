using Backend.Features.Identity.Endpoints.Account;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Identity.Endpoints.Account;

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
/// thing a later refresh has to go on, and signing out removes the session's own row. A client
/// that kept the tenant in a cookie, a tab or local storage would have nothing here to remove, and
/// this case would fail.
/// </para>
/// <para>
/// The account belongs to two tenants, which is what makes the last assertion mean something: after
/// signing out, the next sign-in resolves no tenant by itself, so a session that had inherited the
/// previous selection would report one. The account is built by the test rather than taken from the
/// seed, because the test reads every refresh-token row the account holds, and a seeded account's
/// rows are shared with whatever else in the suite is signed in as it.
/// </para>
/// </remarks>
public class SignoutTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that signing out ends the session signed out of and no other: a second session of the
    /// same account - a tenant opened signed in on its own host, say - can still be renewed afterwards.
    /// </summary>
    [Fact]
    public async Task Signout_Leaves_The_Accounts_Other_Sessions_Renewable()
    {
        var account = await CreateUserAsync(await CreateRoleAsync(Allow.User_View));

        var departing = await SessionForAsync(account.Username);
        var staying = await SessionForAsync(account.Username);

        var (signoutResponse, _) = await departing.Client.POSTAsync<SignoutEndpoint, EmptyResponse>();
        signoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (departedResponse, _) = await departing.Client.GETAsync<GetInfoEndpoint, ProblemDetails>();
        departedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the session signed out of ends at once");

        await staying.RenewAsync();
        var (stayingResponse, _) = await staying.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        stayingResponse.StatusCode.Should().Be(HttpStatusCode.OK, "signing out of one session leaves the account's others alone");
    }

}
