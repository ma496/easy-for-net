using Backend.Features.Identity.Endpoints.Account;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests that signing out ends the session signed out of, and only that one.
/// </summary>
/// <remarks>
/// The account is built by the test rather than taken from the seed, because a seeded account's
/// sessions are shared with whatever else in the suite is signed in as it.
/// </remarks>
public class SignoutTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that signing out ends the session signed out of and no other: a second session of the
    /// same account - one opened in another browser, say - can still be renewed afterwards.
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
