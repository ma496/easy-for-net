namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the notification surfaces as a platform account acting in platform scope sees them: its own
/// platform-scope notifications and the platform-wide ones are read, counted, marked and deleted there,
/// while neither follows the account across the boundary - a platform-scope personal notification is not
/// shown inside a tenant, and one raised inside a tenant is not shown in platform scope.
/// </summary>
/// <remarks>
/// Every test makes its own platform account, a member of a tenant of its own, so that one identity can
/// be asked from both sides of the boundary and nothing another test does to a seeded account's
/// notifications can move the counts read here.
/// </remarks>
public class NotificationPlatformScopeTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// Verifies that a personal notification raised in platform scope is listed and readable in platform
    /// scope and in neither way inside a tenant the same account belongs to.
    /// </summary>
    [Fact]
    public async Task Platform_Personal_Notification_Is_Seen_Only_In_Platform_Scope()
    {
        var (account, tenantId) = await CreatePlatformAccountAsync();
        var notification = await CreatePlatformUserNotificationAsync(account.Id);

        var platformClient = await ClientForAsync(account.Username);
        var tenantClient = await ClientForAsync(account.Username, tenantId);

        (await SearchIdsAsync(platformClient, notification.TitleKey)).Should().ContainSingle()
            .Which.Should().Be(notification.Id, "a notification raised for the account in platform scope is one of its own there");
        (await SearchIdsAsync(tenantClient, notification.TitleKey)).Should().BeEmpty(
            "it names no tenant, but it is addressed to one account in platform scope and does not follow it into a tenant");

        var (platformRsp, platformRes) = await platformClient
            .GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(new() { Id = notification.Id });
        platformRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        platformRes.UserId.Should().Be(account.Id);

        var (tenantRsp, _) = await tenantClient
            .GETAsync<NotificationGetEndpoint, NotificationGetRequest, NotificationGetResponse>(new() { Id = notification.Id });
        tenantRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that a personal notification raised inside a tenant is not shown to the same account while
    /// it acts in platform scope, and that a platform-wide one is shown in both.
    /// </summary>
    [Fact]
    public async Task Tenant_Notifications_Stay_Out_Of_Platform_Scope_While_Platform_Wide_Ones_Reach_It()
    {
        var (account, tenantId) = await CreatePlatformAccountAsync();
        var tenantPersonal = await CreateUserNotificationAsync(account.Id, tenantId: tenantId);
        var tenantWide = await CreateTenantNotificationAsync(tenantId: tenantId);
        var platformWide = await CreateGlobalNotificationAsync();

        var platformClient = await ClientForAsync(account.Username);

        (await SearchIdsAsync(platformClient, tenantPersonal.TitleKey)).Should().BeEmpty(
            "a notification raised for the account inside a tenant is shown only while it acts in that tenant");
        (await SearchIdsAsync(platformClient, tenantWide.TitleKey)).Should().BeEmpty(
            "a notification addressed to a tenant's membership is shown only inside that tenant");
        (await SearchIdsAsync(platformClient, platformWide.TitleKey)).Should().ContainSingle(
            "a platform-wide notification reaches every user in every scope, platform scope included");
    }

    /// <summary>
    /// Verifies that the unread count in platform scope counts the account's own platform-scope
    /// notifications and the platform-wide ones, and nothing raised inside its tenant - and that the count
    /// inside the tenant is not moved by a platform-scope personal notification.
    /// </summary>
    [Fact]
    public async Task Unread_Count_Follows_The_Scope_Acted_In()
    {
        var (account, tenantId) = await CreatePlatformAccountAsync();
        var platformClient = await ClientForAsync(account.Username);
        var tenantClient = await ClientForAsync(account.Username, tenantId);

        var platformBefore = await UnreadCountAsync(platformClient);
        var tenantBefore = await UnreadCountAsync(tenantClient);

        await CreatePlatformUserNotificationAsync(account.Id);
        await CreateUserNotificationAsync(account.Id, tenantId: tenantId);

        (await UnreadCountAsync(platformClient)).Should().Be(platformBefore + 1,
            "in platform scope only the notification raised there counts");
        (await UnreadCountAsync(tenantClient)).Should().Be(tenantBefore + 1,
            "inside the tenant only the notification raised there counts");
    }

    /// <summary>
    /// Verifies that marking a platform-scope personal notification read and unread in platform scope
    /// flips the row's own read flag, and that marking a platform-wide one read records a visit.
    /// </summary>
    [Fact]
    public async Task Mark_As_Read_And_Unread_Work_In_Platform_Scope()
    {
        var (account, _) = await CreatePlatformAccountAsync();
        var personal = await CreatePlatformUserNotificationAsync(account.Id);
        var platformWide = await CreateGlobalNotificationAsync();
        var client = await ClientForAsync(account.Username);

        var (readRsp, _) = await client.POSTAsync<NotificationMarkAsReadEndpoint, NotificationMarkAsReadRequest, NotificationMarkAsReadResponse>(
            new() { Id = personal.Id });
        readRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredNotificationAsync(personal.Id)).IsRead.Should().BeTrue();

        var (unreadRsp, _) = await client.POSTAsync<NotificationMarkAsUnreadEndpoint, NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>(
            new() { Id = personal.Id });
        unreadRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredNotificationAsync(personal.Id)).IsRead.Should().BeFalse();

        var (visitRsp, _) = await client.POSTAsync<NotificationMarkAsReadEndpoint, NotificationMarkAsReadRequest, NotificationMarkAsReadResponse>(
            new() { Id = platformWide.Id });
        visitRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IsVisitedAsync(platformWide.Id, account.Id)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that marking everything read in platform scope marks the account's platform-scope personal
    /// notifications and the platform-wide ones, and leaves what was raised inside its tenant unread.
    /// </summary>
    [Fact]
    public async Task Mark_All_As_Read_In_Platform_Scope_Leaves_Tenant_Notifications_Unread()
    {
        var (account, tenantId) = await CreatePlatformAccountAsync();
        var personal = await CreatePlatformUserNotificationAsync(account.Id);
        var platformWide = await CreateGlobalNotificationAsync();
        var tenantPersonal = await CreateUserNotificationAsync(account.Id, tenantId: tenantId);
        var tenantWide = await CreateTenantNotificationAsync(tenantId: tenantId);
        var client = await ClientForAsync(account.Username);

        var (rsp, _) = await client.POSTAsync<NotificationMarkAllAsReadEndpoint, NotificationMarkAllAsReadResponse>();
        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        (await StoredNotificationAsync(personal.Id)).IsRead.Should().BeTrue();
        (await IsVisitedAsync(platformWide.Id, account.Id)).Should().BeTrue();
        (await StoredNotificationAsync(tenantPersonal.Id)).IsRead.Should().BeFalse(
            "a notification raised inside the tenant is not one platform scope can see, so it is not marked from there");
        (await IsVisitedAsync(tenantWide.Id, account.Id)).Should().BeFalse();
        (await UnreadCountAsync(client)).Should().Be(0, "nothing platform scope shows the account is left unread");
    }

    /// <summary>
    /// Verifies that a platform-scope personal notification can be deleted by its recipient in platform
    /// scope, and that one raised inside its tenant cannot be deleted from there.
    /// </summary>
    [Fact]
    public async Task Delete_In_Platform_Scope_Reaches_Only_Platform_Scope_Rows()
    {
        var (account, tenantId) = await CreatePlatformAccountAsync();
        var personal = await CreatePlatformUserNotificationAsync(account.Id);
        var tenantPersonal = await CreateUserNotificationAsync(account.Id, tenantId: tenantId);
        var client = await ClientForAsync(account.Username);

        var (deleteRsp, _) = await client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = personal.Id });
        deleteRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SearchIdsAsync(client, personal.TitleKey)).Should().BeEmpty();

        var (tenantRsp, _) = await client.DELETEAsync<NotificationDeleteEndpoint, NotificationDeleteRequest, NotificationDeleteResponse>(
            new() { Id = tenantPersonal.Id });
        tenantRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Creates a platform account that is a member of a tenant of its own. Signed in without naming a tenant
    /// it acts in platform scope; signed in naming the tenant it acts inside it.
    /// </summary>
    /// <returns>The account and the tenant it belongs to.</returns>
    private async Task<(User Account, Guid TenantId)> CreatePlatformAccountAsync()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        await MarkAsPlatformAccountAsync(account.Id);
        return (account, tenant.Id);
    }

    /// <summary>
    /// Reads the unread count through a caller's own client, so the scope counted is that caller's.
    /// </summary>
    /// <param name="client">The client presenting the caller's token.</param>
    /// <returns>The unread count the API answered with.</returns>
    private static async Task<int> UnreadCountAsync(HttpClient client)
    {
        var (rsp, res) = await client.GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();
        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        return res.Count;
    }
}
