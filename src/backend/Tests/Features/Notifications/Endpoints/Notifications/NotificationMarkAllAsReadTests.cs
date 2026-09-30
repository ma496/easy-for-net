namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationMarkAllAsReadEndpoint"/> covering marking all notifications as
/// read for the current user, the tenant restriction both of its bulk statements carry, the
/// tenant whose notifications it may mark, and the recipient's other tenants it leaves unread
///.
/// </summary>
/// <remarks>
/// The endpoint marks rows through bulk statements that no tenant filter reaches - one <c>ExecuteUpdate</c>
/// and hand-written <c>INSERT ... ON CONFLICT</c> statements for the read cursors of audience notifications - so
/// the tests here are written against one recipient who is a member of two tenants. That is what makes
/// the restriction observable: the same recipient's rows exist in both, and only the active tenant's may
/// change, which is a claim no single-tenant arrangement could state.
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
    /// one: afterwards the older tenant-wide and platform-wide notifications read as read, while ones raised
    /// after the call read as unread and are counted.
    /// </summary>
    [Fact]
    public async Task Audience_Notifications_Raised_After_The_Call_Are_Unread()
    {
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);
        var olderTenantWide = await CreateTenantNotificationAsync(tenantId: tenant.Id);
        var olderPlatformWide = await CreateGlobalNotificationAsync();
        var client = await ClientForAsync(recipient.Username, tenant.Id);

        var (rsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var newerTenantWide = await CreateTenantNotificationAsync(tenantId: tenant.Id);
        var newerPlatformWide = await CreateGlobalNotificationAsync();

        (await IsReadForAsync(client, olderTenantWide.Id)).Should().BeTrue("the tenant's read cursor covers it");
        (await IsReadForAsync(client, olderPlatformWide.Id)).Should().BeTrue("the platform-wide read cursor covers it");
        (await IsReadForAsync(client, newerTenantWide.Id)).Should().BeFalse("it was raised after the cursor was set");
        (await IsReadForAsync(client, newerPlatformWide.Id)).Should().BeFalse("it was raised after the cursor was set");
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
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);
        var audience = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            audience.Add((await CreateTenantNotificationAsync(tenantId: tenant.Id)).Id);
        }

        audience.Add((await CreateGlobalNotificationAsync()).Id);
        var client = await ClientForAsync(recipient.Username, tenant.Id);

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
    /// Verifies that the bulk statement marking the caller's notifications as read reaches only the rows of
    /// the tenant they are acting in.
    /// </summary>
    /// <remarks>
    /// Several rows are arranged on each side rather than one, because what is under test is a statement
    /// over a set: a restriction applied to the row the statement happened to look at first, rather than
    /// to the statement itself, would leave the rest of the active tenant's rows unread and would be
    /// invisible in a one-row arrangement.
    /// </remarks>
    [Fact]
    public async Task Raw_Statement_Respects_The_Tenant()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(acted.Id, other.Id);

        var inActed = new List<Notification>();
        var inOther = new List<Notification>();
        for (var i = 0; i < 3; i++)
        {
            inActed.Add(await CreateUserNotificationAsync(recipient.Id, tenantId: acted.Id));
            inOther.Add(await CreateUserNotificationAsync(recipient.Id, tenantId: other.Id));
        }

        var client = await ClientForAsync(recipient.Username, acted.Id);

        var (rsp, res) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();

        foreach (var notification in inActed)
        {
            (await StoredNotificationAsync(notification.Id)).IsRead.Should().BeTrue(
                "the statement marks every one of the caller's unread notifications in the tenant it is acting in");
        }

        foreach (var notification in inOther)
        {
            (await StoredNotificationAsync(notification.Id)).IsRead.Should().BeFalse(
                "the same recipient's notification in another tenant is outside the statement's restriction, however many rows of it there are");
        }
    }

    /// <summary>
    /// Verifies that marking everything as read marks exactly the notifications visible in the tenant the
    /// caller acts in, leaving the same recipient's notification in another tenant unread.
    /// </summary>
    /// <remarks>
    /// One recipient is a member of both tenants, so the two rows are the same person's and the only thing
    /// that tells them apart is the tenant each was raised in. Each is arranged unread first and asserted
    /// as it was left, because a call that marked nothing at all would also satisfy "the other tenant's is
    /// still unread".
    /// </remarks>
    [Fact]
    public async Task Marks_Only_The_Active_Tenants_Notifications()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(acted.Id, other.Id);

        var inActed = await CreateUserNotificationAsync(recipient.Id, tenantId: acted.Id);
        var inOther = await CreateUserNotificationAsync(recipient.Id, tenantId: other.Id);

        (await StoredNotificationAsync(inActed.Id)).IsRead.Should().BeFalse("both notifications are unread before the call");
        (await StoredNotificationAsync(inOther.Id)).IsRead.Should().BeFalse("both notifications are unread before the call");

        var client = await ClientForAsync(recipient.Username, acted.Id);

        var (rsp, res) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();

        (await StoredNotificationAsync(inActed.Id)).IsRead.Should().BeTrue(
            "the notification raised in the tenant the caller is acting in is one of the notifications visible to them there, so the call marks it");
        (await StoredNotificationAsync(inOther.Id)).IsRead.Should().BeFalse(
            "the other tenant's notification is not visible in the active tenant, so a call made there does not mark it");
    }

    /// <summary>
    /// Verifies that both bulk statements reach no further than the tenant the caller acts in, so the
    /// recipient's notifications in another tenant stay unread and unvisited.
    /// </summary>
    /// <remarks>
    /// The two halves are exercised together and separately observed: a notification addressed to the
    /// recipient personally is marked through the bulk update, and one addressed to the whole tenant is
    /// marked through the visit insert. The visit the second half would write for another tenant's
    /// audience notification is asserted absent by identifier, since no query filter reaches a
    /// hand-written statement and its tenant predicate is the only thing keeping the row out.
    /// </remarks>
    [Fact]
    public async Task Raw_Statement_Leaves_Other_Tenants_Unread()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(acted.Id, other.Id);

        var personalInActed = await CreateUserNotificationAsync(recipient.Id, tenantId: acted.Id);
        var audienceInActed = await CreateTenantNotificationAsync(tenantId: acted.Id);
        var personalInOther = await CreateUserNotificationAsync(recipient.Id, tenantId: other.Id);
        var audienceInOther = await CreateTenantNotificationAsync(tenantId: other.Id);

        var client = await ClientForAsync(recipient.Username, acted.Id);
        var otherClient = await ClientForAsync(recipient.Username, other.Id);

        var (rsp, res) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();

        (await StoredNotificationAsync(personalInActed.Id)).IsRead.Should().BeTrue(
            "the bulk update marks the caller's own notifications of the tenant being acted in");
        (await IsReadForAsync(client, audienceInActed.Id)).Should().BeTrue(
            "and the cursor upsert covers the active tenant's notification addressed to its whole membership");

        (await StoredNotificationAsync(personalInOther.Id)).IsRead.Should().BeFalse(
            "the same recipient's own notification of another tenant is outside the bulk update's restriction");
        (await IsReadForAsync(otherClient, audienceInOther.Id)).Should().BeFalse(
            "and no cursor is written for another tenant, which is what keeps its notification unread when the recipient acts there");
    }

    /// <summary>
    /// Verifies that calling mark-all-as-read a second time moves the caller's existing read cursors rather
    /// than adding new ones, so the notifications raised between the two calls read as read afterwards.
    /// </summary>
    /// <remarks>
    /// The second call is what reaches the conflict branch of the cursor upserts. The platform-wide cursor
    /// names no tenant, so only a unique index treating nulls as equal lets its upsert find the existing row;
    /// with nulls distinct each call would add another platform-wide cursor. It is run in a tenant, where
    /// both a tenant cursor and the platform-wide one are written, and in platform scope, where only the
    /// platform-wide one is, by a platform account the test creates.
    /// </remarks>
    /// <param name="platformScope">Whether the caller acts in platform scope rather than in its tenant.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Second_Call_Moves_The_Existing_Cursors(bool platformScope)
    {
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);
        if (platformScope)
        {
            await MarkAsPlatformAccountAsync(recipient.Id);
        }

        var client = platformScope
            ? await ClientForAsync(recipient.Username)
            : await ClientForAsync(recipient.Username, tenant.Id);

        var (firstRsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        firstRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstCursors = await CursorsOfAsync(recipient.Id);

        var between = new List<Guid> { (await CreateGlobalNotificationAsync()).Id };
        if (!platformScope)
        {
            between.Add((await CreateTenantNotificationAsync(tenantId: tenant.Id)).Id);
        }

        var (secondRsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        secondRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cursors = await CursorsOfAsync(recipient.Id);

        cursors.Should().ContainSingle(cursor => cursor.TenantId == null,
            "the platform-wide cursor is found by the second upsert and moved, not added again");
        if (platformScope)
        {
            cursors.Should().HaveCount(1, "platform scope writes no tenant cursor");
        }
        else
        {
            cursors.Should().ContainSingle(cursor => cursor.TenantId == tenant.Id, "the tenant's cursor is moved, not added again");
            cursors.Should().HaveCount(2);
        }

        foreach (var cursor in cursors)
        {
            cursor.ReadAllAt.Should().BeAfter(firstCursors.Single(first => first.TenantId == cursor.TenantId).ReadAllAt,
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
    /// Reads a user's read cursors across every tenant, because the platform-wide one names no tenant and
    /// the tenant filter alone would hide it.
    /// </summary>
    /// <param name="userId">The user whose cursors are wanted.</param>
    /// <returns>The cursors as the database holds them.</returns>
    private async Task<List<NotificationReadCursor>> CursorsOfAsync(Guid userId)
        => await DbContext.NotificationReadCursors
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(cursor => cursor.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
}
