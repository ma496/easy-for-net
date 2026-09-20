namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for the per-tenant reach of a notification across the notification surfaces: a notice raised in one
/// tenant is visible only there (AC-052), a platform-wide one is visible in every tenant and distinguishable
/// from a tenant-wide one (AC-054), and a notice raised in one tenant is neither listed nor counted in
/// another while the platform-wide one is (AC-129).
/// </summary>
/// <remarks>
/// Every notice here is raised through <see cref="INotificationService"/> rather than arranged directly,
/// because what is under test is that attribution follows the tenant an operation is carried out in and is
/// not something a caller supplies - which only the service's own write can show. One recipient is a member
/// of both tenants throughout, so the two views differ by the tenant being acted in and by nothing else.
/// </remarks>
public class NotificationTenancyTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// The service the notices are raised through, resolved from the running host so that it writes through
    /// the same <see cref="AppTestsBase.DbContext"/> and reads the same tenant scope the test establishes.
    /// </summary>
    private INotificationService NotificationService => Service<INotificationService>();

    /// <summary>
    /// Verifies that a notification raised in one tenant is visible to a recipient acting there and not to
    /// the same recipient acting in another tenant (AC-052).
    /// </summary>
    /// <remarks>
    /// The tenant it was attributed to is read off the stored row as well as off the two views, so that
    /// "absent from the other tenant" is shown to be the attribution rather than a notice that was never
    /// written or was dropped from both.
    /// </remarks>
    [Fact]
    public async Task Tenant_Notification_Is_Visible_Only_In_Its_Tenant()
    {
        var raised = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(raised.Id, other.Id);

        var titleKey = NewTitleKey();

        using (TenantContext.BeginTenant(raised.Id))
        {
            await NotificationService.NewTenantNotificationAsync(
                NotificationType.Info, titleKey, $"{titleKey}.message", cancellationToken: TestContext.Current.CancellationToken);
        }

        var whileActingInRaised = await SearchIdsAsync(await ClientForAsync(recipient.Username, raised.Id), titleKey);
        var whileActingInOther = await SearchIdsAsync(await ClientForAsync(recipient.Username, other.Id), titleKey);

        whileActingInRaised.Should().ContainSingle("the notice was raised in the tenant the recipient is acting in, so it is one of theirs there");

        (await StoredNotificationAsync(whileActingInRaised[0])).TenantId.Should().Be(raised.Id,
            "the notice belongs to the tenant it was raised in, which is what the other view is withheld by");

        whileActingInOther.Should().BeEmpty(
            "the same recipient acting in another tenant is not shown it, so a tenant's notices stay inside that tenant");
    }

    /// <summary>
    /// Verifies that a platform-wide notification is visible while acting in either of two tenants, and is
    /// told apart from a tenant-wide one by naming no tenant at all (AC-054).
    /// </summary>
    /// <remarks>
    /// The recipient is a member of both tenants and the notice is raised outside any tenant scope, which is
    /// the only standing it can be raised from. Its stored attribution is asserted null rather than merely
    /// left unexamined: that is what makes it distinguishable from the notice a tenant raises to its own
    /// membership, and what explains why it is not withheld in the second tenant.
    /// </remarks>
    [Fact]
    public async Task Platform_Wide_Notification_Is_Visible_In_Every_Tenant()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(first.Id, second.Id);

        var titleKey = NewTitleKey();

        await NotificationService.NewGlobalNotificationAsync(
            NotificationType.Info, titleKey, $"{titleKey}.message", cancellationToken: TestContext.Current.CancellationToken);

        var inFirst = await SearchIdsAsync(await ClientForAsync(recipient.Username, first.Id), titleKey);
        var inSecond = await SearchIdsAsync(await ClientForAsync(recipient.Username, second.Id), titleKey);

        inFirst.Should().ContainSingle("a platform-wide notice is visible to everyone, so the recipient sees it while acting in their first tenant");
        inSecond.Should().ContainSingle("and in their second, which is the whole of what addressing the platform means");

        inSecond.Should().BeEquivalentTo(inFirst, "both views answer with the same notice rather than with two of them");

        (await StoredNotificationAsync(inFirst[0])).TenantId.Should().BeNull(
            "it names no tenant, which is what tells it apart from a notice addressed to one tenant's membership");
    }

    /// <summary>
    /// Verifies that a notification raised in one tenant is neither listed nor counted while its recipient
    /// acts in another, while the platform-wide one is both (AC-129).
    /// </summary>
    /// <remarks>
    /// The count is read as a delta around a second notice raised in the same tenant, because the unread
    /// count also includes the platform-wide notices every caller sees and an absolute reading would be
    /// asserting on rows this test did not raise. The platform-wide notice is looked for in both tenants
    /// alongside the tenant-scoped one being looked for in one, so "not listed in the other tenant" is
    /// weighed against something that is.
    /// </remarks>
    [Fact]
    public async Task Notification_In_One_Tenant_Is_Neither_Listed_Nor_Counted_In_Another()
    {
        var raised = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var recipient = await CreateDualTenantMemberAsync(raised.Id, other.Id);

        var tenantKey = NewTitleKey();
        var platformKey = NewTitleKey();

        using (TenantContext.BeginTenant(raised.Id))
        {
            await NotificationService.NewTenantNotificationAsync(
                NotificationType.Info, tenantKey, $"{tenantKey}.message", cancellationToken: TestContext.Current.CancellationToken);
        }

        await NotificationService.NewGlobalNotificationAsync(
            NotificationType.Info, platformKey, $"{platformKey}.message", cancellationToken: TestContext.Current.CancellationToken);

        var actedClient = await ClientForAsync(recipient.Username, raised.Id);
        var otherClient = await ClientForAsync(recipient.Username, other.Id);

        (await SearchIdsAsync(actedClient, tenantKey)).Should().ContainSingle(
            "the notice raised in this tenant is one of the notifications listed here");
        (await SearchIdsAsync(otherClient, tenantKey)).Should().BeEmpty(
            "and it is not listed in the other tenant the recipient acts in");

        (await SearchIdsAsync(actedClient, platformKey)).Should().ContainSingle(
            "the platform-wide notice is listed while acting in this tenant");
        (await SearchIdsAsync(otherClient, platformKey)).Should().ContainSingle(
            "and equally while acting in the other, so what the assertion above measured is the tenant rather than a list that answers nothing");

        var (actedBeforeRsp, actedBefore) = await actedClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();
        var (otherBeforeRsp, otherBefore) = await otherClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        actedBeforeRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        otherBeforeRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        using (TenantContext.BeginTenant(raised.Id))
        {
            await NotificationService.NewTenantNotificationAsync(
                NotificationType.Info, NewTitleKey(), $"test.message.tenancy.{Guid.NewGuid()}", cancellationToken: TestContext.Current.CancellationToken);
        }

        var (actedAfterRsp, actedAfter) = await actedClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();
        var (otherAfterRsp, otherAfter) = await otherClient
            .GETAsync<NotificationGetUnreadCountEndpoint, NotificationGetUnreadCountResponse>();

        actedAfterRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        otherAfterRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        actedAfter.Count.Should().Be(actedBefore.Count + 1,
            "an unread notice raised in the tenant being acted in is counted there, so the reading is a live one");
        otherAfter.Count.Should().Be(otherBefore.Count,
            "while the same notice is not counted at all in the other tenant, so the count is taken over the tenant being acted in");
    }

    /// <summary>
    /// A title key no other test can collide with, in the shape the list's search can be pointed at.
    /// </summary>
    /// <returns>The key.</returns>
    private static string NewTitleKey() => $"test.title.tenancy.{Guid.NewGuid()}";
}
