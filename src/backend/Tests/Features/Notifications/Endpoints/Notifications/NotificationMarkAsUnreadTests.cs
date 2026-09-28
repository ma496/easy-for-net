namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationMarkAsUnreadEndpoint"/> covering marking user and global notifications as unread.
/// </summary>
public class NotificationMarkAsUnreadTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that a user notification can be marked as unread (IsRead = false).
    /// </summary>
    [Fact]
    public async Task MarkAsUnread_UserNotification()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateUserNotificationAsync(userId);
        notification.IsRead = true;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (rsp, res) = await Client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();
        res.Id.Should().Be(notification.Id);

        DbContext.ChangeTracker.Clear();
        // Read across every tenant: the row carries the tenant it was raised in and the lookup here is
        // about the row itself rather than about who may see it while acting where.
        var updated = await DbContext.Notifications
            .AcrossAllTenants()
            .FirstOrDefaultAsync(x => x.Id == notification.Id, cancellationToken: TestContext.Current.CancellationToken);
        updated!.IsRead.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a global notification can be marked as unread by removing the visit record for the current user.
    /// </summary>
    [Fact]
    public async Task MarkAsUnread_GlobalNotification()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateGlobalNotificationAsync();
        await MarkNotificationVisitedAsync(notification.Id, userId);

        var (rsp, res) = await Client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();

        DbContext.ChangeTracker.Clear();
        var isVisited = await DbContext.NotificationVisits.AnyAsync(x => x.NotificationId == notification.Id && x.UserId == userId, cancellationToken: TestContext.Current.CancellationToken);
        isVisited.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that marking an already-unread user notification still returns success.
    /// </summary>
    [Fact]
    public async Task MarkAsUnread_AlreadyUnread_UserNotification()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification = await CreateUserNotificationAsync(userId);
        notification.IsRead = false;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (rsp, res) = await Client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = notification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that marking another user's notification as unread returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task MarkAsUnread_OtherUserNotification_Should_NotFound()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<User>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex);
        var newUser = await CreateAdminUserAsync($"testuser-{faker.Generate().Username}", TestUsers.DefaultPassword);
        var otherUserNotification = await CreateUserNotificationAsync(newUser.Id);

        var (rsp, _) = await Client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = otherUserNotification.Id });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that marking a non-existent notification as unread returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task MarkAsUnread_NonExistent_Notification()
    {
        await SetAuthTokenAsync();

        var (rsp, _) = await Client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = Guid.NewGuid() });

        rsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that unauthenticated requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task MarkAsUnread_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await Client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = Guid.NewGuid() });

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
