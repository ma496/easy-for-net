namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationGetGroupsEndpoint"/> covering retrieval of notification groups.
/// </summary>
public class NotificationGetGroupsTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that groups are correctly populated when notifications have group names assigned.
    /// </summary>
    [Fact]
    public async Task GetGroups_WithNotifications()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification1 = await CreateUserNotificationAsync(userId);
        notification1.Group = "group-a";
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notification2 = await CreateUserNotificationAsync(userId);
        notification2.Group = "group-b";
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateUserNotificationAsync(userId);

        var (rsp, res) = await App.Client.GETAsync<NotificationGetGroupsEndpoint, NotificationGetGroupsResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Groups.Should().Contain("group-a");
        res.Groups.Should().Contain("group-b");
    }

    /// <summary>
    /// Verifies that duplicate group names are returned only once (distinct).
    /// </summary>
    [Fact]
    public async Task GetGroups_DistinctGroups()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification1 = await CreateUserNotificationAsync(userId);
        notification1.Group = "group-a";
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notification2 = await CreateUserNotificationAsync(userId);
        notification2.Group = "group-a";
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (rsp, res) = await App.Client.GETAsync<NotificationGetGroupsEndpoint, NotificationGetGroupsResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Groups.Should().Contain("group-a");
        res.Groups.Count(g => g == "group-a").Should().Be(1);
    }

    /// <summary>
    /// Verifies that groups are returned in ascending alphabetical order.
    /// </summary>
    [Fact]
    public async Task GetGroups_Sorted()
    {
        await SetAuthTokenAsync();

        var userId = TestUsers.TenantAdminUserId;
        var notification1 = await CreateUserNotificationAsync(userId);
        notification1.Group = "zebra";
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notification2 = await CreateUserNotificationAsync(userId);
        notification2.Group = "apple";
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (rsp, res) = await App.Client.GETAsync<NotificationGetGroupsEndpoint, NotificationGetGroupsResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Groups.Should().BeInAscendingOrder();
    }
}
