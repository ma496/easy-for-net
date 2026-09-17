namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationListEndpoint"/> covering listing, pagination, filtering by read status and group, authorization,
/// and the tenant whose notifications the list is answered from (AC-052).
/// </summary>
public class NotificationListTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that user notifications appear in the list with correct title and message keys.
    /// </summary>
    [Fact]
    public async Task List_Notifications_WithUserNotifications()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateUserNotificationAsync(userId);

        var (rsp, res) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                // Asking for everything rather than for a very large page: the arranged notification is
                // an unread one, so it sorts ahead of the read ones, but the platform-wide notifications
                // every caller sees are in the same list and a page can only hold so many of them.
                All = true
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Items.Should().Contain(x => x.Id == notification.Id);
        res.Items.Should().Contain(x => x.TitleKey == notification.TitleKey);
        res.Items.Should().Contain(x => x.MessageKey == notification.MessageKey);
    }

    /// <summary>
    /// Verifies that global notifications appear in the list with no user ID and correct details.
    /// </summary>
    [Fact]
    public async Task List_Notifications_WithGlobalNotifications()
    {
        await SetAuthTokenAsync();

        var notification = await CreateGlobalNotificationAsync();

        var (rsp, res) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 100
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Items.Should().Contain(x => x.Id == notification.Id);
        var item = res.Items.First(x => x.Id == notification.Id);
        item.UserId.Should().BeNull();
        item.TitleKey.Should().Be(notification.TitleKey);
        item.MessageKey.Should().Be(notification.MessageKey);
        item.IsRead.Should().BeFalse();
        item.Group.Should().Be(notification.Group);
        item.Type.Should().Be(notification.Type);
    }

    /// <summary>
    /// Verifies that pagination works correctly for notification listing.
    /// </summary>
    [Fact]
    public async Task List_Notifications_Pagination()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        for (var i = 0; i < 5; i++)
        {
            await CreateUserNotificationAsync(userId);
        }

        var (rsp, res) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 2
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Items.Count.Should().Be(2);

        var (page2Rsp, page2Res) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 2,
                PageSize = 2
            });

        page2Rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Res.Items.Count.Should().Be(2);
    }

    /// <summary>
    /// Verifies that filtering by <c>IsRead</c> returns only notifications matching the specified read status.
    /// </summary>
    [Fact]
    public async Task List_Notifications_FilterByIsRead()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var unread = await CreateUserNotificationAsync(userId);
        unread.IsRead = false;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var readNotification = await CreateUserNotificationAsync(userId);
        readNotification.IsRead = true;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (unreadRsp, unreadRes) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 100,
                IsRead = false
            });

        unreadRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        unreadRes.Items.Should().AllSatisfy(x => x.IsRead.Should().BeFalse());

        var (readRsp, readRes) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 100,
                IsRead = true
            });

        readRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        readRes.Items.Should().AllSatisfy(x => x.IsRead.Should().BeTrue());
    }

    /// <summary>
    /// Verifies that filtering by group name returns only notifications belonging to that group.
    /// </summary>
    [Fact]
    public async Task List_Notifications_FilterByGroup()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var groupName = "test-group";

        var grouped = await CreateUserNotificationAsync(userId);
        grouped.Group = groupName;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateUserNotificationAsync(userId);

        var (rsp, res) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 100,
                Group = groupName
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Items.Should().AllSatisfy(x => x.Group.Should().Be(groupName));
    }

    /// <summary>
    /// Verifies that a notification raised in one tenant is listed only while its recipient acts in that
    /// tenant, so one tenant's notifications stay out of another's (AC-052).
    /// </summary>
    /// <remarks>
    /// One recipient is a member of both tenants, so what the two views differ by is the tenant being
    /// acted in and nothing else - the same account, the same two notifications. Each notification is
    /// looked for in its own tenant as well as in the other's, because a list that answered nothing to
    /// everybody would satisfy "absent from the other tenant" without being restricted at all.
    /// </remarks>
    [Fact]
    public async Task Notification_Of_Another_Tenant_Is_Not_Listed()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(acted.Id, other.Id);

        var inActed = await CreateUserNotificationAsync(recipient.Id, tenantId: acted.Id);
        var inOther = await CreateUserNotificationAsync(recipient.Id, tenantId: other.Id);

        var actedClient = await ClientForAsync(recipient.Username, acted.Id);
        var otherClient = await ClientForAsync(recipient.Username, other.Id);

        var inActedWhileActingInActed = await SearchIdsAsync(actedClient, inActed.TitleKey);
        var inOtherWhileActingInActed = await SearchIdsAsync(actedClient, inOther.TitleKey);
        var inActedWhileActingInOther = await SearchIdsAsync(otherClient, inActed.TitleKey);
        var inOtherWhileActingInOther = await SearchIdsAsync(otherClient, inOther.TitleKey);

        inActedWhileActingInActed.Should().Contain(inActed.Id,
            "the notification was raised in the tenant the recipient is acting in, so it is one of the notifications they see there");
        inOtherWhileActingInOther.Should().Contain(inOther.Id,
            "and the other tenant's notification is likewise one of theirs there, so neither tenant is an empty view");

        inOtherWhileActingInActed.Should().BeEmpty(
            "a notification raised in another tenant is not listed, which is what keeps one tenant's notifications out of another's");
        inActedWhileActingInOther.Should().BeEmpty(
            "and the restriction runs both ways, so there is no tenant whose notifications are the visible ones");
    }

    /// <summary>
    /// Verifies that unauthenticated list requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task List_Notifications_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await App.Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 10
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
