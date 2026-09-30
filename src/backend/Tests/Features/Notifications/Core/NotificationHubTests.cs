namespace Backend.Tests.Features.Notifications.Core;

using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Push;
using Backend.Features.Notifications.Endpoints.Notifications;
using Backend.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebSockets;
using Backend.Tests.Fakes;
using Backend.Tests.Features.Notifications.Endpoints.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Tests for the notification hub: which connections a committed notification is pushed to, that nothing
/// is pushed for a rolled-back one, the read-state push, the per-account cap, and the hub's authentication -
/// the query-string token honoured on the hub's path alone, and refused exactly as HTTP refuses it.
/// </summary>
/// <remarks>
/// Every connection is a real <c>HubConnection</c> over the test server's WebSocket. "Did not arrive" is
/// never shown by sleeping: <see cref="HubProbe.DrainAsync"/> sends a sentinel to the connection after the
/// push under test, and messages to one connection arrive in order, so once the sentinel is in, the push
/// would have been too. The class sits in the <c>Notifications</c> collection through its base, because a
/// platform-wide notification it raises reaches every account's unread count.
/// </remarks>
public class NotificationHubTests(App app) : NotificationsTestsBase(app)
{
    private INotificationService NotificationService => Service<INotificationService>();

    private INotificationHubSender Sender => Service<INotificationHubSender>();

    /// <summary>
    /// A personal notification raised in a tenant reaches its recipient's connection acting in that tenant,
    /// and neither the same account's connection acting in another tenant nor another member's.
    /// </summary>
    [Fact]
    public async Task Personal_Notification_Reaches_Its_Recipient_In_Its_Scope_Only()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        var otherMember = await CreateTenantUserAsync(tenant.Id);

        await using var recipientHere = await ConnectAsync(recipient.Username, recipient.Id, tenant.Id);
        await using var recipientElsewhere = await ConnectAsync(recipient.Username, recipient.Id, otherTenant.Id);
        await using var otherMemberHere = await ConnectAsync(otherMember.Username, otherMember.Id, tenant.Id);

        var titleKey = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewUserNotificationAsync(
                recipient.Id, NotificationType.Warning, titleKey, $"{titleKey}.message", "inventory", "{\"qty\":5}",
                TestContext.Current.CancellationToken);
        }

        var received = await recipientHere.WaitForNotificationAsync(message => message.TitleKey == titleKey);
        var stored = await NotificationByTitleAsync(titleKey);
        received.Id.Should().Be(stored.Id);
        received.Type.Should().Be(NotificationType.Warning);
        received.MessageKey.Should().Be($"{titleKey}.message");
        received.Group.Should().Be("inventory");
        received.Metadata.Should().Be("{\"qty\":5}");
        received.CreatedAt.Should().BeCloseTo(stored.CreatedAt, TimeSpan.FromMilliseconds(1));
        received.IsRead.Should().BeFalse();

        await AssertNotReceivedAsync(recipientElsewhere, titleKey,
            "a personal notification raised in one tenant is not visible while its recipient acts in another");
        await AssertNotReceivedAsync(otherMemberHere, titleKey,
            "a personal notification is not pushed to another member of the tenant");
    }

    /// <summary>
    /// A tenant-wide notification reaches every member acting in that tenant and no connection acting in
    /// another tenant or in platform scope; a platform-wide notification reaches all of them.
    /// </summary>
    [Fact]
    public async Task Tenant_Wide_Reaches_The_Tenant_And_Platform_Wide_Reaches_Everyone()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var first = await CreateTenantUserAsync(tenant.Id);
        var second = await CreateTenantUserAsync(tenant.Id);
        var stranger = await CreateTenantUserAsync(otherTenant.Id);
        var platformAccount = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(platformAccount.Id);

        await using var firstProbe = await ConnectAsync(first.Username, first.Id, tenant.Id);
        await using var secondProbe = await ConnectAsync(second.Username, second.Id, tenant.Id);
        await using var strangerProbe = await ConnectAsync(stranger.Username, stranger.Id, otherTenant.Id);
        await using var platformProbe = await ConnectAsync(platformAccount.Username, platformAccount.Id, tenantId: null);

        var tenantTitle = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewTenantNotificationAsync(
                NotificationType.Info, tenantTitle, $"{tenantTitle}.message", cancellationToken: TestContext.Current.CancellationToken);
        }

        await firstProbe.WaitForNotificationAsync(message => message.TitleKey == tenantTitle);
        await secondProbe.WaitForNotificationAsync(message => message.TitleKey == tenantTitle);
        await AssertNotReceivedAsync(strangerProbe, tenantTitle, "a tenant-wide notification stays inside its tenant");
        await AssertNotReceivedAsync(platformProbe, tenantTitle, "a tenant-wide notification is not visible in platform scope");

        var globalTitle = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewGlobalNotificationAsync(
                NotificationType.Info, globalTitle, $"{globalTitle}.message", cancellationToken: TestContext.Current.CancellationToken);
        }

        foreach (var probe in new[] { firstProbe, secondProbe, strangerProbe, platformProbe })
        {
            await probe.WaitForNotificationAsync(message => message.TitleKey == globalTitle);
        }
    }

    /// <summary>
    /// A notification raised inside a transaction is pushed only when the transaction commits, and never
    /// when it rolls back.
    /// </summary>
    [Fact]
    public async Task Push_Waits_For_Commit_And_Is_Dropped_On_Rollback()
    {
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);
        await using var probe = await ConnectAsync(recipient.Username, recipient.Id, tenant.Id);

        var rolledBackTitle = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await NotificationService.NewUserNotificationAsync(
                recipient.Id, NotificationType.Info, rolledBackTitle, $"{rolledBackTitle}.message", cancellationToken: TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await AssertNotReceivedAsync(probe, rolledBackTitle, "a notification whose transaction rolled back does not exist to be shown");

        var committedTitle = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await NotificationService.NewUserNotificationAsync(
                recipient.Id, NotificationType.Info, committedTitle, $"{committedTitle}.message", cancellationToken: TestContext.Current.CancellationToken);

            await AssertNotReceivedAsync(probe, committedTitle, "nothing is pushed while the transaction is still open");

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await probe.WaitForNotificationAsync(message => message.TitleKey == committedTitle);
    }

    /// <summary>
    /// A transaction disposed without committing drops its pushes exactly as a rollback does.
    /// </summary>
    [Fact]
    public async Task Push_Is_Dropped_When_The_Transaction_Is_Disposed_Uncommitted()
    {
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);
        await using var probe = await ConnectAsync(recipient.Username, recipient.Id, tenant.Id);

        var abandonedTitle = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await using (await DbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                await NotificationService.NewUserNotificationAsync(
                    recipient.Id, NotificationType.Info, abandonedTitle, $"{abandonedTitle}.message", cancellationToken: TestContext.Current.CancellationToken);
            }

            // The context's next transaction commits; the abandoned one's push must not ride along with it.
            DbContext.ChangeTracker.Clear();
            var committedTitle = NewTitleKey();
            await using (var transaction = await DbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                await NotificationService.NewUserNotificationAsync(
                    recipient.Id, NotificationType.Info, committedTitle, $"{committedTitle}.message", cancellationToken: TestContext.Current.CancellationToken);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await probe.WaitForNotificationAsync(message => message.TitleKey == committedTitle);
        }

        probe.Notifications.Should().NotContain(message => message.TitleKey == abandonedTitle);
    }

    /// <summary>
    /// A raise whose push fails still saves the notification and does not throw.
    /// </summary>
    [Fact]
    public async Task Failed_Push_Does_Not_Fail_The_Raise()
    {
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);

        var publisher = new NotificationPublisher(
            DbContext, Service<NotificationCommitInterceptor>(), new ThrowingHubSender(), NullLogger<NotificationPublisher>.Instance);
        var service = new NotificationService(DbContext, TenantContext, publisher);

        var titleKey = NewTitleKey();
        var inTransactionTitle = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            var raise = () => service.NewUserNotificationAsync(
                recipient.Id, NotificationType.Info, titleKey, $"{titleKey}.message", cancellationToken: TestContext.Current.CancellationToken);
            await raise.Should().NotThrowAsync();

            await using var transaction = await DbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await service.NewUserNotificationAsync(
                recipient.Id, NotificationType.Info, inTransactionTitle, $"{inTransactionTitle}.message", cancellationToken: TestContext.Current.CancellationToken);
            var commit = () => transaction.CommitAsync(TestContext.Current.CancellationToken);
            await commit.Should().NotThrowAsync("a push deferred to the commit that fails is logged, not thrown into the commit");
        }

        (await NotificationByTitleAsync(titleKey)).UserId.Should().Be(recipient.Id);
        (await NotificationByTitleAsync(inTransactionTitle)).UserId.Should().Be(recipient.Id);
    }

    /// <summary>
    /// The batched raise saves one row per distinct recipient and pushes each to its own recipient only.
    /// </summary>
    [Fact]
    public async Task NewUserNotifications_Reaches_Each_Recipient()
    {
        var tenant = await CreateTenantAsync();
        var first = await CreateTenantUserAsync(tenant.Id);
        var second = await CreateTenantUserAsync(tenant.Id);
        var bystander = await CreateTenantUserAsync(tenant.Id);

        await using var firstProbe = await ConnectAsync(first.Username, first.Id, tenant.Id);
        await using var secondProbe = await ConnectAsync(second.Username, second.Id, tenant.Id);
        await using var bystanderProbe = await ConnectAsync(bystander.Username, bystander.Id, tenant.Id);

        var titleKey = NewTitleKey();
        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewUserNotificationsAsync(
                [first.Id, second.Id, first.Id], NotificationType.Success, titleKey, $"{titleKey}.message",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var stored = await DbContext.Notifications
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(notification => notification.TitleKey == titleKey)
            .ToListAsync(TestContext.Current.CancellationToken);
        stored.Select(notification => notification.UserId).Should().BeEquivalentTo(new Guid?[] { first.Id, second.Id },
            "a recipient named twice is addressed once");
        stored.Should().OnlyContain(notification => notification.TenantId == tenant.Id);

        var toFirst = await firstProbe.WaitForNotificationAsync(message => message.TitleKey == titleKey);
        var toSecond = await secondProbe.WaitForNotificationAsync(message => message.TitleKey == titleKey);
        toFirst.Id.Should().Be(stored.Single(notification => notification.UserId == first.Id).Id);
        toSecond.Id.Should().Be(stored.Single(notification => notification.UserId == second.Id).Id);

        await AssertNotReceivedAsync(bystanderProbe, titleKey, "a member who was not named is not told");
        await firstProbe.DrainAsync(Sender);
        firstProbe.Notifications.Count(message => message.TitleKey == titleKey).Should().Be(1, "one push per recipient");
    }

    /// <summary>
    /// Marking everything read pushes the caller's recomputed count to their other connection in that scope,
    /// and to nobody else's.
    /// </summary>
    [Fact]
    public async Task UnreadCountChanged_Reaches_The_Callers_Other_Connection_After_Mark_All_As_Read()
    {
        var tenant = await CreateTenantAsync();
        var caller = await CreateTenantUserAsync(tenant.Id);
        var otherMember = await CreateTenantUserAsync(tenant.Id);
        await CreateTenantNotificationAsync(tenantId: tenant.Id);

        var callerClient = await ClientForAsync(caller.Username, tenant.Id);
        (await UnreadCountAsync(callerClient)).Should().BeGreaterThan(0);

        await using var callersOtherConnection = await ConnectAsync(caller.Username, caller.Id, tenant.Id);
        await using var otherMembersConnection = await ConnectAsync(otherMember.Username, otherMember.Id, tenant.Id);

        var (response, _) = await callerClient.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pushed = await callersOtherConnection.WaitForUnreadCountAsync();
        pushed.Count.Should().Be(0);
        pushed.Count.Should().Be(await UnreadCountAsync(callerClient), "the pushed count is the one the API reports");

        await otherMembersConnection.DrainAsync(Sender);
        otherMembersConnection.UnreadCounts.Should().BeEmpty("a caller's read state is nobody else's business");
    }

    /// <summary>
    /// Marking one notification read, then unread again, then deleting it, pushes the caller's count each time.
    /// </summary>
    [Fact]
    public async Task UnreadCountChanged_Follows_Mark_As_Read_And_Unread_And_Delete()
    {
        var tenant = await CreateTenantAsync();
        var caller = await CreateTenantUserAsync(tenant.Id);
        var notification = await CreateUserNotificationAsync(caller.Id, tenantId: tenant.Id);
        var callerClient = await ClientForAsync(caller.Username, tenant.Id);
        var unreadBefore = await UnreadCountAsync(callerClient);

        await using var probe = await ConnectAsync(caller.Username, caller.Id, tenant.Id);

        var (readResponse, _) = await callerClient.POSTAsync<NotificationMarkAsReadEndpoint, NotificationMarkAsReadRequest, NotificationMarkAsReadResponse>(new() { Id = notification.Id });
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await probe.WaitForUnreadCountAsync()).Count.Should().Be(unreadBefore - 1);

        var (unreadResponse, _) = await callerClient.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(new() { Id = notification.Id });
        unreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitUntilAsync(() => probe.UnreadCounts.Count >= 2);
        probe.UnreadCounts.Last().Count.Should().Be(unreadBefore);

        var (deleteResponse, _) = await callerClient.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(new() { Id = notification.Id });
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitUntilAsync(() => probe.UnreadCounts.Count >= 3);
        probe.UnreadCounts.Last().Count.Should().Be(unreadBefore - 1, "the deleted notification was unread");
        probe.UnreadCounts.Last().Count.Should().Be(await UnreadCountAsync(callerClient));
    }

    /// <summary>
    /// A connection with no credential, and one whose session was deleted, are refused with 401 - as an HTTP
    /// request with the same credential is.
    /// </summary>
    [Fact]
    public async Task Unauthenticated_And_Ended_Session_Are_Refused_With_401()
    {
        await using (var anonymous = HubProbe.Create(App.Server, accessToken: null, Guid.Empty, tenantId: null))
        {
            var start = () => anonymous.Connection.StartAsync(TestContext.Current.CancellationToken);
            (await start.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("401");
        }

        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var token = await TokenForAsync(account.Username, tenant.Id);
        await SessionStore.DeleteAsync(SessionIdOf(token), TestContext.Current.CancellationToken);

        await using var ended = HubProbe.Create(App.Server, token, account.Id, tenant.Id);
        var startEnded = () => ended.Connection.StartAsync(TestContext.Current.CancellationToken);
        (await startEnded.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("401");

        using var http = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(http, token);
        var (httpResponse, _) = await http.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();
        httpResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the hub refuses exactly what HTTP refuses");
    }

    /// <summary>
    /// A connection whose session cannot be looked up because the store is down is refused with 503, never
    /// admitted and never answered as anonymous.
    /// </summary>
    [Fact]
    public async Task Session_Store_Outage_Is_Refused_With_503()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var token = await TokenForAsync(account.Username, tenant.Id);
        Service<SessionStoreFaults>().MakeUnreachable(SessionIdOf(token));

        await using var probe = HubProbe.Create(App.Server, token, account.Id, tenant.Id);
        var start = () => probe.Connection.StartAsync(TestContext.Current.CancellationToken);
        (await start.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("503");
    }

    /// <summary>
    /// An access token in the query string authenticates the hub's path and nothing else: an HTTP endpoint
    /// called with a valid <c>access_token</c> and no header answers 401.
    /// </summary>
    [Fact]
    public async Task Query_String_Token_Is_Ignored_Off_The_Hub_Path()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var token = await TokenForAsync(account.Username, tenant.Id);
        var url = $"{IEndpoint.TestURLFor<NotificationGetUnreadCountEndpoint>()}?access_token={Uri.EscapeDataString(token)}";

        using var anonymous = App.CreateClient(new ClientOptions { HandleCookies = false });
        var refused = await anonymous.GetAsync(url, TestContext.Current.CancellationToken);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var bearer = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(bearer, token);
        var admitted = await bearer.GetAsync(url, TestContext.Current.CancellationToken);
        admitted.StatusCode.Should().Be(HttpStatusCode.OK, "the same token in the header is a valid credential");

        // And on the hub's path the same query-string token is accepted.
        await using var probe = await ConnectWithTokenAsync(token, account.Id, tenant.Id);
        probe.Connection.State.Should().Be(Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected);
    }

    /// <summary>
    /// The hub serves WebSockets alone: negotiation offers nothing else, and a long-polling or server-sent
    /// events request for the connection it negotiated is not served.
    /// </summary>
    [Fact]
    public async Task Long_Polling_And_Server_Sent_Events_Are_Refused()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        using var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        TestsHelper.SetAuthToken(client, await TokenForAsync(account.Username, tenant.Id));

        var negotiate = await client.PostAsync($"{NotificationHub.Path}/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);
        negotiate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var negotiation = System.Text.Json.JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        negotiation.RootElement.GetProperty("availableTransports").EnumerateArray()
            .Select(transport => transport.GetProperty("transport").GetString())
            .Should().Equal("WebSockets");
        var connectionToken = negotiation.RootElement.GetProperty("connectionToken").GetString();

        var longPoll = await client.GetAsync($"{NotificationHub.Path}?id={connectionToken}", TestContext.Current.CancellationToken);
        longPoll.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var sseRequest = new HttpRequestMessage(HttpMethod.Get, $"{NotificationHub.Path}?id={connectionToken}");
        sseRequest.Headers.Accept.ParseAdd("text/event-stream");
        var sse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        sse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// An account at <see cref="NotificationOptions.MaxConnectionsPerUser"/> connections on this instance has
    /// its next connection refused, and gets the place back once one closes.
    /// </summary>
    [Fact]
    public async Task Connection_Over_The_Cap_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var token = await TokenForAsync(account.Username, tenant.Id);
        var registry = Service<NotificationConnectionRegistry>();
        var cap = Service<IOptions<NotificationOptions>>().Value.MaxConnectionsPerUser;

        var probes = new List<HubProbe>();
        try
        {
            for (var index = 0; index < cap; index++)
            {
                probes.Add(await ConnectWithTokenAsync(token, account.Id, tenant.Id));
            }

            registry.CountOf(account.Id).Should().Be(cap);

            await using var overCap = HubProbe.Create(App.Server, token, account.Id, tenant.Id);
            try
            {
                await overCap.Connection.StartAsync(TestContext.Current.CancellationToken);
            }
            catch (Exception)
            {
                // Closed may already have been observed during start; the close reason below is what is asserted.
            }

            var closedWith = await overCap.Closed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            closedWith.Should().NotBeNull();
            closedWith!.Message.Should().Contain(NotificationHub.ConnectionLimitExceeded);
            registry.CountOf(account.Id).Should().Be(cap, "the refused connection took no place");

            // Disposing a client does not wait for the server to run OnDisconnectedAsync, so the place is
            // waited for rather than asserted.
            await probes[0].DisposeAsync();
            probes.RemoveAt(0);
            await WaitUntilAsync(() => registry.CountOf(account.Id) == cap - 1);

            probes.Add(await ConnectWithTokenAsync(token, account.Id, tenant.Id));
            registry.CountOf(account.Id).Should().Be(cap);
        }
        finally
        {
            foreach (var probe in probes)
            {
                await probe.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// A personal notification raised in platform scope reaches the account's platform-scope connection and
    /// not the same account's connection acting in a tenant it belongs to.
    /// </summary>
    [Fact]
    public async Task Personal_Notification_In_Platform_Scope_Reaches_The_Platform_Connection_Only()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        await MarkAsPlatformAccountAsync(account.Id);

        await using var inPlatform = await ConnectAsync(account.Username, account.Id, tenantId: null);
        await using var inTenant = await ConnectAsync(account.Username, account.Id, tenant.Id);

        var titleKey = NewTitleKey();
        using (TenantContext.BeginPlatformScope())
        {
            await NotificationService.NewUserNotificationAsync(
                account.Id, NotificationType.Info, titleKey, $"{titleKey}.message", cancellationToken: TestContext.Current.CancellationToken);
        }

        var received = await inPlatform.WaitForNotificationAsync(message => message.TitleKey == titleKey);
        received.Id.Should().Be((await NotificationByTitleAsync(titleKey)).Id);
        await AssertNotReceivedAsync(inTenant, titleKey,
            "a personal notification raised in platform scope is visible only while its recipient acts in platform scope");
    }

    /// <summary>
    /// The WebSocket middleware SignalR runs in front of the hub, given the host's own options, refuses with
    /// 403 an upgrade whose <c>Origin</c> is not one of the web app's domains - so a page on another host
    /// cannot open the hub with its visitor's cookie - and passes one from an allowed origin, and one with no
    /// <c>Origin</c> at all (a non-browser bearer client), on to the hub.
    /// </summary>
    /// <remarks>
    /// The middleware is driven directly rather than through <see cref="HubProbe"/>: the test server's
    /// WebSocket client supplies its own WebSocket feature, and the middleware checks the origin only when it
    /// is the one supplying it, so over the test server every origin would be let through.
    /// </remarks>
    [Theory]
    [InlineData("https://sibling.example.invalid", StatusCodes.Status403Forbidden, false)]
    [InlineData(null, StatusCodes.Status200OK, true)]
    public async Task Upgrade_Origin_Is_Checked_Against_The_Web_Domains(string? origin, int expectedStatus, bool expectedToReachHub)
        => await AssertUpgradeAsync(origin, expectedStatus, expectedToReachHub);

    /// <summary>
    /// Every one of the web app's own domains is an allowed WebSocket origin.
    /// </summary>
    [Fact]
    public async Task Upgrade_From_Each_Web_Domain_Reaches_The_Hub()
    {
        foreach (var domain in Service<IOptions<WebSetting>>().Value.AllowedDomains())
        {
            await AssertUpgradeAsync(domain, StatusCodes.Status200OK, expectedToReachHub: true);
        }
    }

    /// <summary>A title key no other test uses.</summary>
    private static string NewTitleKey() => $"test.hub.{Guid.NewGuid():N}";

    /// <summary>Signs an account in on a client of its own and returns its access token.</summary>
    private async Task<string> TokenForAsync(string username, Guid? tenantId)
    {
        using var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        return await TestsHelper.GetNewAuthTokenAsync(client, username, TestUsers.DefaultPassword, await TenantIdentifierOfAsync(tenantId));
    }

    /// <summary>Signs an account in and connects to the hub as it.</summary>
    private async Task<HubProbe> ConnectAsync(string username, Guid userId, Guid? tenantId)
        => await ConnectWithTokenAsync(await TokenForAsync(username, tenantId), userId, tenantId);

    /// <summary>Connects to the hub with a token already issued.</summary>
    private async Task<HubProbe> ConnectWithTokenAsync(string token, Guid userId, Guid? tenantId)
        => await HubProbe.ConnectAsync(App.Server, token, userId, tenantId);

    /// <summary>The session an access token names.</summary>
    private static string SessionIdOf(string accessToken)
        => TestsHelper.PayloadOf(accessToken)["sid"].GetString()!;

    /// <summary>
    /// Asserts that a push for a title never reached a connection, by draining the connection first.
    /// </summary>
    private async Task AssertNotReceivedAsync(HubProbe probe, string titleKey, string because)
    {
        await probe.DrainAsync(Sender);
        probe.Notifications.Should().NotContain(message => message.TitleKey == titleKey, because);
    }

    /// <summary>The stored notification with a title key, across every tenant.</summary>
    private async Task<Backend.Features.Notifications.Core.Entities.Notification> NotificationByTitleAsync(string titleKey)
        => await DbContext.Notifications
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(notification => notification.TitleKey == titleKey, TestContext.Current.CancellationToken);

    /// <summary>Waits, bounded, for a condition that is expected to become true.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true within 10 seconds.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Runs one WebSocket upgrade request through <see cref="WebSocketMiddleware"/> with the host's options and
    /// asserts whether it was refused or passed on.
    /// </summary>
    private async Task AssertUpgradeAsync(string? origin, int expectedStatus, bool expectedToReachHub)
    {
        var reachedHub = false;
        var middleware = new WebSocketMiddleware(
            _ =>
            {
                reachedHub = true;
                return Task.CompletedTask;
            },
            Service<IOptions<Microsoft.AspNetCore.Builder.WebSocketOptions>>(),
            NullLoggerFactory.Instance);

        var context = new DefaultHttpContext();
        context.Features.Set<IHttpUpgradeFeature>(new UpgradableRequest());
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = NotificationHub.Path;
        context.Request.Headers.Connection = "Upgrade";
        context.Request.Headers.Upgrade = "websocket";
        context.Request.Headers.SecWebSocketVersion = "13";
        context.Request.Headers.SecWebSocketKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(expectedStatus, $"an upgrade from origin '{origin ?? "(none)"}'");
        reachedHub.Should().Be(expectedToReachHub);
    }

    /// <summary>An upgrade feature that reports the request as upgradable, as Kestrel's does.</summary>
    private sealed class UpgradableRequest : IHttpUpgradeFeature
    {
        public bool IsUpgradableRequest => true;

        public Task<Stream> UpgradeAsync() => throw new InvalidOperationException("The test never accepts the upgrade.");
    }

    /// <summary>A hub sender that always fails, standing in for a backplane that is down.</summary>
    private sealed class ThrowingHubSender : INotificationHubSender
    {
        public Task SendAsync(string group, string method, object message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The hub is unreachable (test double).");
    }
}
