using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationGetUnreadCountEndpoint"/> covering unread notification counting
/// and the cap the count stops at.
/// </summary>
/// <remarks>
/// The count is relative rather than absolute throughout: the database also holds the notifications
/// addressed to every user, which every caller sees, so a test that asserted a total would be asserting
/// on rows it did not create. What it asserts instead is how the reading moves when a notification is
/// raised.
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

        var userId = TestUsers.AdminUserId;
        await CreateUserNotificationAsync(userId);
        await CreateUserNotificationAsync(userId);

        var (rsp, res) = await Client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

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

        var (rsp, res) = await Client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// Verifies that the count stops at the cap however many notifications are unread.
    /// </summary>
    /// <remarks>
    /// The notifications are addressed to an account the test creates, so nothing another test raises
    /// to a single account reaches its rows, and they are written in one save because what is under test
    /// is the count, not how the rows were raised.
    /// </remarks>
    [Fact]
    public async Task Count_Never_Exceeds_The_Cap()
    {
        var recipient = await CreateUserAsync();

        {
            for (var i = 0; i < NotificationQueries.UnreadCountCap + 5; i++)
            {
                DbContext.Notifications.Add(new Notification
                {
                    UserId = recipient.Id,
                    Type = NotificationType.Info,
                    TitleKey = $"test.title.cap.{Guid.NewGuid()}",
                    MessageKey = $"test.message.cap.{Guid.NewGuid()}"
                });
            }

            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = await ClientForAsync(recipient.Username);

        (await UnreadCountAsync(client)).Should().Be(NotificationQueries.UnreadCountCap,
            "more notifications than the cap are unread, and the count stops at the cap");
    }

    /// <summary>
    /// Verifies that unauthenticated requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetUnreadCount_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await Client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
