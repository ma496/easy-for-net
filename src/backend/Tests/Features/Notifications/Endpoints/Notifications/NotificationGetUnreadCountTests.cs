namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationGetUnreadCountEndpoint"/> covering unread notification counting
/// in general and the tenant the count is taken over (AC-056).
/// </summary>
/// <remarks>
/// The count is relative rather than absolute throughout: notifications are counted per tenant and the
/// database also holds the platform-wide ones every caller sees, so a test that asserted a total would
/// be asserting on rows it did not create. What it asserts instead is how the reading moves when a
/// notification is raised.
/// </remarks>
public class NotificationGetUnreadCountTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that unread user notifications are counted correctly.
    /// </summary>
    [Fact]
    public async Task GetUnreadCount_WithUnreadUserNotifications()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        await CreateUserNotificationAsync(userId);
        await CreateUserNotificationAsync(userId);

        var (rsp, res) = await App.Client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// Verifies that unread global notifications are counted correctly.
    /// </summary>
    [Fact]
    public async Task GetUnreadCount_WithGlobalUnreadNotifications()
    {
        await SetAuthTokenAsync();

        await CreateGlobalNotificationAsync();
        await CreateGlobalNotificationAsync();

        var (rsp, res) = await App.Client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// Verifies that the count is taken over the tenant being acted in, so a notification raised in
    /// another tenant does not move it (AC-056).
    /// </summary>
    /// <remarks>
    /// Two recipients' worth of proof are given here: the count taken in the tenant being acted in is
    /// shown to move when a notification is raised there, so "unmoved" is not a reading that never moves
    /// at all; and the notification raised in the other tenant is shown to be counted while the same
    /// recipient acts in that tenant, so what the first half measured is attribution rather than a row
    /// that was never written.
    /// </remarks>
    [Fact]
    public async Task Counts_Only_The_Active_Tenant()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(acted.Id, other.Id);

        var actedClient = await ClientForAsync(recipient.Username, acted.Id);
        var otherClient = await ClientForAsync(recipient.Username, other.Id);

        var (beforeRsp, before) = await actedClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        beforeRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        await CreateUserNotificationAsync(recipient.Id, tenantId: acted.Id);

        var (raisedRsp, raised) = await actedClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        raisedRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        raised.Count.Should().Be(before.Count + 1,
            "an unread notification raised in the tenant being acted in is what the badge counts, so the reading is live rather than fixed");

        var (otherBeforeRsp, otherBefore) = await otherClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        otherBeforeRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        await CreateUserNotificationAsync(recipient.Id, tenantId: other.Id);

        var (otherAfterRsp, otherAfter) = await otherClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        otherAfterRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        otherAfter.Count.Should().Be(otherBefore.Count + 1,
            "the notification is counted while the recipient acts in the tenant it belongs to, so it is a real unread notification and not a write that went nowhere");

        var (afterRsp, after) = await actedClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        afterRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        after.Count.Should().Be(raised.Count,
            "and the count taken in the other tenant is unchanged by it, so what the reading is taken over is the tenant the recipient acts in");
    }

    /// <summary>
    /// Verifies that unauthenticated requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetUnreadCount_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await App.Client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
