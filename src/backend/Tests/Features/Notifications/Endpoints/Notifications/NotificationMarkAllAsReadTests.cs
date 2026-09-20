namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the <see cref="NotificationMarkAllAsReadEndpoint"/> covering marking all notifications as
/// read for the current user, the tenant restriction both of its bulk statements carry (AC-033), the
/// tenant whose notifications it may mark (AC-055), and the recipient's other tenants it leaves unread
/// (AC-130).
/// </summary>
/// <remarks>
/// The endpoint marks rows through two statements that no query filter reaches - one <c>ExecuteUpdate</c>
/// and one hand-written <c>INSERT ... ON CONFLICT</c> for the visit rows of audience notifications - so
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
        (await IsVisitedAsync(globalNotification.Id, newUser.Id)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the bulk statement marking the caller's notifications as read reaches only the rows of
    /// the tenant they are acting in (AC-033).
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
    /// caller acts in, leaving the same recipient's notification in another tenant unread (AC-055).
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
    /// recipient's notifications in another tenant stay unread and unvisited (AC-130).
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

        var (rsp, res) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Success.Should().BeTrue();

        (await StoredNotificationAsync(personalInActed.Id)).IsRead.Should().BeTrue(
            "the bulk update marks the caller's own notifications of the tenant being acted in");
        (await IsVisitedAsync(audienceInActed.Id, recipient.Id)).Should().BeTrue(
            "and the visit insert reaches the active tenant's notification addressed to its whole membership");

        (await StoredNotificationAsync(personalInOther.Id)).IsRead.Should().BeFalse(
            "the same recipient's own notification of another tenant is outside the bulk update's restriction");
        (await IsVisitedAsync(audienceInOther.Id, recipient.Id)).Should().BeFalse(
            "and no visit is written for a notification of another tenant, which is what keeps it unread when the recipient acts there");
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
}
