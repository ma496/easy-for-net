namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationDeleteEndpoint"/> covering deletion of user notifications, global notifications, and authorization.
/// </summary>
public class NotificationDeleteTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that a user notification can be successfully deleted by its owner.
    /// </summary>
    [Fact]
    public async Task Delete_UserNotification()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateUserNotificationAsync(userId);

        var (rsp, res) = await App.Client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();
        res.Id.Should().Be(notification.Id);

        DbContext.ChangeTracker.Clear();
        // Read across every tenant: the row carries the tenant it was raised in and the lookup here is
        // about whether it still exists at all, not about who may see it while acting where.
        var deleted = await DbContext.Notifications
            .AcrossAllTenants()
            .FirstOrDefaultAsync(x => x.Id == notification.Id, cancellationToken: TestContext.Current.CancellationToken);
        deleted.Should().BeNull();
    }

    /// <summary>
    /// Verifies that deleting another user's notification returns 404 NotFound (users cannot delete others' notifications).
    /// </summary>
    [Fact]
    public async Task Delete_OtherUserNotification_Should_NotFound()
    {
        await SetAuthTokenAsync();

        var otherUserNotification = await CreateUserNotificationAsync(TestUsers.TestUserId);

        var (rsp, _) = await App.Client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = otherUserNotification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that deleting a non-existent notification returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Delete_NonExistent_Notification()
    {
        await SetAuthTokenAsync();

        var (rsp, _) = await App.Client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = Guid.NewGuid() });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that deleting a global notification returns 404 NotFound (global notifications cannot be deleted by users).
    /// </summary>
    [Fact]
    public async Task Delete_GlobalNotification_Should_NotAllowed()
    {
        await SetAuthTokenAsync();

        var globalNotification = await CreateGlobalNotificationAsync();

        var (rsp, _) = await App.Client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = globalNotification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that unauthenticated delete requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task Delete_Unauthenticated()
    {
        ClearAuthToken();
        
        var (rsp, _) = await App.Client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = Guid.NewGuid() });

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
