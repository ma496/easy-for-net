using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Notifications.Core.Push;
using Backend.Tests.Fakes;
using Backend.Tests.Features.Notifications.Endpoints.Notifications;
using Microsoft.AspNetCore.SignalR.Client;

namespace Backend.Tests.Features.Notifications.Core;

/// <summary>
/// Tests that a notification hub connection is closed the moment the session that authenticated it ends -
/// revoked, signed out, replaced by a refresh - and that a connection of the
/// same account on a session that did not end stays open.
/// </summary>
/// <remarks>
/// Every connection is a real <c>HubConnection</c> opened through <see cref="HubProbe.ConnectAsync"/>, so it
/// has joined its groups before the test acts. Closure is awaited on the connection's <c>Closed</c> event with
/// a bounded wait; "still open" is shown by draining the connection - a sentinel pushed to it arrives - after
/// the other session's connection has closed. Every account is the test's own. The class sits in
/// the <c>Notifications</c> collection through its base, beside the other hub tests.
/// </remarks>
public class NotificationSessionEndTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>How long a connection is given to close once its session has ended.</summary>
    private static readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(10);

    private INotificationHubSender Sender => Service<INotificationHubSender>();

    /// <summary>
    /// Revoking an account's sessions closes every connection they authenticated, and leaves another
    /// account's connection open.
    /// </summary>
    [Fact]
    public async Task Revoking_A_User_Closes_Every_Connection_Of_Their_Sessions()
    {
        var account = await CreateUserAsync();
        var bystander = await CreateUserAsync();

        await using var first = await ConnectAsync(await TokenForAsync(account.Username), account.Id);
        await using var second = await ConnectAsync(await TokenForAsync(account.Username), account.Id);
        await using var other = await ConnectAsync(await TokenForAsync(bystander.Username), bystander.Id);

        await Service<ISessionRevocationService>().RevokeUserAsync(account.Id, TestContext.Current.CancellationToken);

        await AssertClosedAsync(first);
        await AssertClosedAsync(second);
        await AssertOpenAsync(other);
    }

    /// <summary>
    /// Signing out closes the connection of the session signed out of, and leaves the connection of the same
    /// account's other session open.
    /// </summary>
    [Fact]
    public async Task Signout_Closes_The_Connection_Of_Its_Session_Only()
    {
        var account = await CreateUserAsync();
        var signedOutToken = await TokenForAsync(account.Username);

        await using var signedOut = await ConnectAsync(signedOutToken, account.Id);
        await using var kept = await ConnectAsync(await TokenForAsync(account.Username), account.Id);

        using var http = ClientWith(signedOutToken);
        var (response, _) = await http.POSTAsync<SignoutEndpoint, EmptyResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertClosedAsync(signedOut);
        await AssertOpenAsync(kept);
    }

    /// <summary>
    /// A refresh replaces the session it renews, closing the connection the replaced session authenticated;
    /// the renewed token connects as before.
    /// </summary>
    [Fact]
    public async Task Refresh_Closes_The_Connection_Of_The_Replaced_Session()
    {
        var account = await CreateUserAsync();
        var session = await SessionForAsync(account.Username);

        await using var replaced = await ConnectAsync(AccessTokenOf(session.Client), account.Id);

        var renewedToken = await session.RenewAsync();

        await AssertClosedAsync(replaced);
        await using var renewed = await ConnectAsync(renewedToken, account.Id);
        await AssertOpenAsync(renewed);
    }

    /// <summary>
    /// A handler that throws when the session is announced as ended fails neither the sign-out nor a
    /// revocation, and the hub's own handler still closes the connections.
    /// </summary>
    [Fact]
    public async Task A_Failing_Handler_Fails_Neither_Signout_Nor_Revocation()
    {
        var account = await CreateUserAsync();
        var faults = Service<SessionEndedHandlerFaults>();

        var signedOutToken = await TokenForAsync(account.Username);
        var revokedToken = await TokenForAsync(account.Username);
        faults.FailFor(SessionIdOf(signedOutToken));
        faults.FailFor(SessionIdOf(revokedToken));

        await using var signedOut = await ConnectAsync(signedOutToken, account.Id);
        await using var revoked = await ConnectAsync(revokedToken, account.Id);

        using var http = ClientWith(signedOutToken);
        var (response, _) = await http.POSTAsync<SignoutEndpoint, EmptyResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "a failing session-ended handler never fails the sign-out");
        await AssertClosedAsync(signedOut);

        var revoke = () => Service<ISessionRevocationService>().RevokeUserAsync(account.Id, TestContext.Current.CancellationToken);
        await revoke.Should().NotThrowAsync("a failing session-ended handler never fails a revocation");
        await AssertClosedAsync(revoked);
    }

    /// <summary>Signs an account in on a client of its own and returns its access token.</summary>
    private async Task<string> TokenForAsync(string username)
    {
        using var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        return await TestsHelper.GetNewAuthTokenAsync(client, username, TestUsers.DefaultPassword);
    }

    /// <summary>A client presenting exactly this bearer token.</summary>
    private HttpClient ClientWith(string accessToken)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(client, accessToken);
        return client;
    }

    private Task<HubProbe> ConnectAsync(string accessToken, Guid userId)
        => HubProbe.ConnectAsync(App.Server, accessToken, userId);

    private static string AccessTokenOf(HttpClient client) => client.DefaultRequestHeaders.Authorization!.Parameter!;

    private static string SessionIdOf(string accessToken)
        => TestsHelper.PayloadOf(accessToken)["sid"].GetString()!;

    /// <summary>Waits, bounded, for the server to close the connection.</summary>
    private static async Task AssertClosedAsync(HubProbe probe)
    {
        var close = () => probe.Closed.WaitAsync(_closeTimeout, TestContext.Current.CancellationToken);
        await close.Should().NotThrowAsync("the connection's session ended, so the server closes it");
        probe.Connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    /// <summary>
    /// Shows the connection is still open and delivering: a sentinel pushed to it now arrives, and it has not
    /// been closed.
    /// </summary>
    private async Task AssertOpenAsync(HubProbe probe)
    {
        await probe.DrainAsync(Sender);
        probe.Closed.IsCompleted.Should().BeFalse("the connection's own session did not end");
        probe.Connection.State.Should().Be(HubConnectionState.Connected);
    }
}
