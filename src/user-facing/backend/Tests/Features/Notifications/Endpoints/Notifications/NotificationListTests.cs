using Backend.Features.Notifications.Endpoints.Notifications;

namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationListEndpoint"/> covering listing, pagination, filtering by read status and group,
/// and authorization.
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

        var userId = TestUsers.AdminUserId;
        var notification = await CreateUserNotificationAsync(userId);

        var (rsp, res) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                // Asking for everything rather than for a very large page: the arranged notification is
                // an unread one, so it sorts ahead of the read ones, but the notifications addressed to
                // every user are in the same list and a page can only hold so many of them.
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

        var (rsp, res) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
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

        var userId = TestUsers.AdminUserId;
        for (var i = 0; i < 5; i++)
        {
            await CreateUserNotificationAsync(userId);
        }

        var (rsp, res) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 2
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Items.Count.Should().Be(2);

        var (page2Rsp, page2Res) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
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

        var userId = TestUsers.AdminUserId;
        var unread = await CreateUserNotificationAsync(userId);
        unread.IsRead = false;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var readNotification = await CreateUserNotificationAsync(userId);
        readNotification.IsRead = true;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (unreadRsp, unreadRes) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 100,
                IsRead = false
            });

        unreadRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        unreadRes.Items.Should().AllSatisfy(x => x.IsRead.Should().BeFalse());

        var (readRsp, readRes) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
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

        var userId = TestUsers.AdminUserId;
        var groupName = "test-group";

        var grouped = await CreateUserNotificationAsync(userId);
        grouped.Group = groupName;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateUserNotificationAsync(userId);

        var (rsp, res) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
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
    /// Verifies that unauthenticated list requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task List_Notifications_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await Client.GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
            new()
            {
                Page = 1,
                PageSize = 10
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
