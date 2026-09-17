namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationGetEndpoint"/> covering retrieval of user and global notifications, read tracking, and authorization.
/// </summary>
public class NotificationGetTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that a user notification can be retrieved by ID with the correct title and message keys.
    /// </summary>
    [Fact]
    public async Task Get_UserNotification()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateUserNotificationAsync(userId);

        var (rsp, res) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Id.Should().Be(notification.Id);
        res.TitleKey.Should().Be(notification.TitleKey);
        res.MessageKey.Should().Be(notification.MessageKey);
    }

    /// <summary>
    /// Verifies that a global notification can be retrieved and has no user ID assigned.
    /// </summary>
    [Fact]
    public async Task Get_GlobalNotification()
    {
        await SetAuthTokenAsync();

        var notification = await CreateGlobalNotificationAsync();

        var (rsp, res) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Id.Should().Be(notification.Id);
        res.UserId.Should().BeNull();
    }

    /// <summary>
    /// Verifies that requesting a non-existent notification returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_NonExistent_Notification()
    {
        await SetAuthTokenAsync();

        var (rsp, _) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = Guid.NewGuid() });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that accessing another user's notification returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_OtherUserNotification_Should_NotFound()
    {
        await SetAuthTokenAsync();

        var otherUserNotification = await CreateUserNotificationAsync(TestUsers.TestUserId);

        var (rsp, _) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = otherUserNotification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that a global notification is initially marked as unread (IsRead = false) when not yet visited.
    /// </summary>
    [Fact]
    public async Task Get_GlobalNotification_Unvisited()
    {
        await SetAuthTokenAsync();

        var notification = await CreateGlobalNotificationAsync();

        var (rsp, res) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.IsRead.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a global notification is marked as read (IsRead = true) after being visited by the user.
    /// </summary>
    [Fact]
    public async Task Get_GlobalNotification_Visited()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateGlobalNotificationAsync();
        await MarkNotificationVisitedAsync(notification.Id, userId);

        var (rsp, res) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.IsRead.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that unauthenticated get requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task Get_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await App.Client.GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(
            new() { Id = Guid.NewGuid() });

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
