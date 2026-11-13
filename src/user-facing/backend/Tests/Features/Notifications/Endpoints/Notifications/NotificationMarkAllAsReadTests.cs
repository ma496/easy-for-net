using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationMarkAllAsReadEndpoint"/> covering marking all notifications as
/// read for the current user, the read cursor that carries it for the notifications addressed to every
/// user, and the notifications raised after the call that it leaves unread.
/// </summary>
/// <remarks>
/// The endpoint marks rows through bulk statements - one <c>ExecuteUpdate</c> and hand-written
/// <c>INSERT ... ON CONFLICT</c> statements for the read cursors of audience notifications - so what they
/// changed is read back from the database or through the API rather than from the change tracker.
/// </remarks>
public class NotificationMarkAllAsReadTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that marking all notifications as read updates user notifications to IsRead = true and records visits for global notifications.
    /// </summary>
    [Fact]
    public async Task MarkAllAsRead_Success()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<User>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex);
        var newUser = await CreateAdminUserAsync($"testuser-{faker.Generate().Username}", TestUsers.DefaultPassword);
        var userNotification1 = await CreateUserNotificationAsync(newUser.Id);
        var userNotification2 = await CreateUserNotificationAsync(newUser.Id);
        var globalNotification = await CreateGlobalNotificationAsync();

        await SetAuthTokenAsync(newUser.Username, TestUsers.DefaultPassword);

        var (rsp, res) = await Client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();

        (await StoredNotificationAsync(userNotification1.Id)).IsRead.Should().BeTrue();
        (await StoredNotificationAsync(userNotification2.Id)).IsRead.Should().BeTrue();
        (await IsReadForAsync(Client, globalNotification.Id)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that mark-all-as-read covers the audience notifications raised up to the call and no later
    /// one: afterwards the older notifications addressed to every user read as read, while ones raised
    /// after the call read as unread and are counted.
    /// </summary>
    [Fact]
    public async Task Audience_Notifications_Raised_After_The_Call_Are_Unread()
    {
        var recipient = await CreateUserAsync();
        var olderFirst = await CreateGlobalNotificationAsync();
        var olderSecond = await CreateGlobalNotificationAsync();
        var client = await ClientForAsync(recipient.Username);

        var (rsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var newerFirst = await CreateGlobalNotificationAsync();
        var newerSecond = await CreateGlobalNotificationAsync();

        (await IsReadForAsync(client, olderFirst.Id)).Should().BeTrue("the read cursor covers it");
        (await IsReadForAsync(client, olderSecond.Id)).Should().BeTrue("the read cursor covers it");
        (await IsReadForAsync(client, newerFirst.Id)).Should().BeFalse("it was raised after the cursor was set");
        (await IsReadForAsync(client, newerSecond.Id)).Should().BeFalse("it was raised after the cursor was set");
        (await UnreadCountAsync(client)).Should().Be(2, "only the two notifications raised after the call are unread");
    }

    /// <summary>
    /// Verifies that mark-all-as-read writes no per-notification rows: however many audience notifications
    /// it covers, the caller is left holding no visit rows, the ones they had included.
    /// </summary>
    /// <remarks>
    /// Two visits are arranged beforehand - one read, one marked unread - to show that both kinds are removed
    /// once the cursor covers them: a read one would be redundant and an unread one would contradict the call.
    /// </remarks>
    [Fact]
    public async Task Visit_Rows_Do_Not_Grow_With_The_Audience_Notifications()
    {
        var recipient = await CreateUserAsync();
        var audience = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            audience.Add((await CreateGlobalNotificationAsync()).Id);
        }

        audience.Add((await CreateGlobalNotificationAsync()).Id);
        var client = await ClientForAsync(recipient.Username);

        (await client.POSTAsync<NotificationMarkAsReadEndpoint, NotificationMarkAsReadRequest, NotificationMarkAsReadResponse>(
            new() { Id = audience[0] })).Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = audience[1] })).Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await VisitCountAsync(recipient.Id)).Should().Be(2, "one read and one unread visit were arranged");

        var (rsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        (await VisitCountAsync(recipient.Id)).Should().Be(0,
            "read state after mark-all-as-read is carried by the read cursors, not by one row per notification");

        foreach (var notificationId in audience)
        {
            (await IsReadForAsync(client, notificationId)).Should().BeTrue("every audience notification up to the call is read");
        }

        (await UnreadCountAsync(client)).Should().Be(0);
    }

    /// <summary>
    /// Verifies that calling mark-all-as-read a second time moves the caller's existing read cursors rather
    /// than adding new ones, so the notifications raised between the two calls read as read afterwards.
    /// </summary>
    /// <remarks>
    /// The second call is what reaches the conflict branch of the cursor upsert: the user's one cursor is
    /// found and moved rather than added again.
    /// </remarks>
    [Fact]
    public async Task Second_Call_Moves_The_Existing_Cursor()
    {
        var recipient = await CreateUserAsync();
        var client = await ClientForAsync(recipient.Username);

        var (firstRsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        firstRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstCursors = await CursorsOfAsync(recipient.Id);

        var between = new List<Guid> { (await CreateGlobalNotificationAsync()).Id };

        var (secondRsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        secondRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cursors = await CursorsOfAsync(recipient.Id);

        cursors.Should().ContainSingle("the cursor is found by the second upsert and moved, not added again");

        foreach (var cursor in cursors)
        {
            cursor.ReadAllAt.Should().BeAfter(firstCursors.Single().ReadAllAt,
                "the second call moves each cursor forward");
        }

        foreach (var notificationId in between)
        {
            (await IsReadForAsync(client, notificationId)).Should().BeTrue("the second call's cursor covers what was raised between the calls");
        }

        (await UnreadCountAsync(client)).Should().Be(0);
    }

    /// <summary>
    /// Verifies that unauthenticated requests return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task MarkAllAsRead_Unauthenticated()
    {
        ClearAuthToken();

        var (rsp, _) = await Client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Reads a user's read cursors straight from the database, since no endpoint reports them.
    /// </summary>
    /// <param name="userId">The user whose cursors are wanted.</param>
    /// <returns>The cursors as the database holds them.</returns>
    private async Task<List<NotificationReadCursor>> CursorsOfAsync(Guid userId)
        => await DbContext.NotificationReadCursors
            .AsNoTracking()
            .Where(cursor => cursor.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
}
