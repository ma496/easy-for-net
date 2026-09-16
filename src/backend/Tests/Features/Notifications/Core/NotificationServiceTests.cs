namespace Backend.Tests.Features.Notifications.Core;

using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Tests.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for <see cref="INotificationService"/> covering the two tenant-scoped addressing modes - a single
/// member of a tenant and every member of one (AC-053) - and the platform-wide mode that stays
/// distinguishable from a tenant-wide one (AC-054).
/// </summary>
/// <remarks>
/// The rows the service writes are read twice over: once as the row it persisted, which is where the
/// attribution and the audience are legible, and once through the list a recipient is answered from, which
/// is where "addressed to them" is observable. Only the second half can tell a row attributed to the right
/// tenant from one attributed to nobody.
/// </remarks>
public class NotificationServiceTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>
    /// The service under test, resolved from the running host so that it writes through the same
    /// <see cref="AppTestsBase.DbContext"/> and the same tenant scope the fixture's own arrangement uses.
    /// </summary>
    private INotificationService NotificationService => App.Services.GetRequiredService<INotificationService>();

    /// <summary>
    /// Verifies that a notification addressed to a single member of a tenant reaches that member and no
    /// other member of the same tenant (AC-053).
    /// </summary>
    /// <remarks>
    /// The tenant has a second member, which is what makes the addressing meaningful: every member of a
    /// tenant is a recipient of a tenant-wide notification, so a single-member one is only shown to be
    /// single-member by a member who does not get it.
    /// </remarks>
    [Fact]
    public async Task Addresses_A_Single_Member()
    {
        var tenant = await CreateTenantAsync();
        var recipient = await CreateTenantUserAsync(tenant.Id);
        var otherMember = await CreateTenantUserAsync(tenant.Id);

        var titleKey = NewTitleKey();

        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewUserNotificationAsync(
                recipient.Id, NotificationType.Info, titleKey, $"{titleKey}.message");
        }

        var stored = await NotificationByTitleAsync(titleKey);
        stored.TenantId.Should().Be(tenant.Id,
            "a notice raised while acting in a tenant is attributed to that tenant, and to nothing the caller supplied");
        stored.UserId.Should().Be(recipient.Id,
            "and it names the member it was addressed to, which is what tells it apart from one addressed to the whole tenant");

        var recipientIds = await SearchIdsAsync(await ClientForAsync(recipient.Username), titleKey);
        recipientIds.Should().Contain(stored.Id, "the member it names is one of the members it reaches");

        var otherMemberIds = await SearchIdsAsync(await ClientForAsync(otherMember.Username), titleKey);
        otherMemberIds.Should().BeEmpty(
            "a notification addressed to one member is not addressed to the others, however they share the tenant it was raised in");
    }

    /// <summary>
    /// Verifies that a notification addressed to a tenant reaches every member of it and nobody outside it
    /// (AC-053).
    /// </summary>
    /// <remarks>
    /// Two members of the tenant and one member of another are each asked: the two inside are what the
    /// addressing claims, and the one outside is what keeps "visible to both" from being satisfied by a
    /// notification that is visible to everybody.
    /// </remarks>
    [Fact]
    public async Task Addresses_Every_Member_Of_The_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var outside = await CreateTenantAsync();
        var first = await CreateTenantUserAsync(tenant.Id);
        var second = await CreateTenantUserAsync(tenant.Id);
        var stranger = await CreateTenantUserAsync(outside.Id);

        var titleKey = NewTitleKey();

        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewTenantNotificationAsync(
                NotificationType.Info, titleKey, $"{titleKey}.message");
        }

        var stored = await NotificationByTitleAsync(titleKey);
        stored.TenantId.Should().Be(tenant.Id, "the tenant it was raised in is the tenant it belongs to");
        stored.UserId.Should().BeNull(
            "a notification addressed to a whole membership names no single recipient - that is how each member's read state stays their own");

        var firstIds = await SearchIdsAsync(await ClientForAsync(first.Username), titleKey);
        firstIds.Should().Contain(stored.Id, "every member of the tenant is a recipient of it");

        var secondIds = await SearchIdsAsync(await ClientForAsync(second.Username), titleKey);
        secondIds.Should().Contain(stored.Id, "and every member means every member, not the one who happened to be named first");

        var strangerIds = await SearchIdsAsync(await ClientForAsync(stranger.Username), titleKey);
        strangerIds.Should().BeEmpty(
            "a member of another tenant is not one of its recipients, so the notice does not follow them into their own tenant");
    }

    /// <summary>
    /// Verifies that a notification addressed to the whole platform stays distinguishable from one
    /// addressed to a tenant's membership (AC-054).
    /// </summary>
    /// <remarks>
    /// The two rows are raised through the two methods and compared to each other: what tells them apart is
    /// that one names no tenant at all and the other names the tenant it was raised in. Their read state is
    /// kept the same way for both, so the distinction would be invisible if the attribution were not
    /// recorded - which is exactly why it is the assertion rather than a comment.
    /// </remarks>
    [Fact]
    public async Task Keeps_A_Platform_Wide_Notification_Distinguishable_From_A_Tenant_Wide_One()
    {
        var tenant = await CreateTenantAsync();

        var platformKey = NewTitleKey();
        var tenantKey = NewTitleKey();

        await NotificationService.NewGlobalNotificationAsync(
            NotificationType.Info, platformKey, $"{platformKey}.message");

        using (TenantContext.BeginTenant(tenant.Id))
        {
            await NotificationService.NewTenantNotificationAsync(
                NotificationType.Info, tenantKey, $"{tenantKey}.message");
        }

        var platformWide = await NotificationByTitleAsync(platformKey);
        var tenantWide = await NotificationByTitleAsync(tenantKey);

        platformWide.TenantId.Should().BeNull(
            "a notification addressed to every user of the platform belongs to no tenant, which is what keeps it from becoming one tenant's");
        tenantWide.TenantId.Should().Be(tenant.Id,
            "while one addressed to a tenant's membership carries that tenant, and the two are told apart by exactly that");

        platformWide.UserId.Should().BeNull("neither addresses a single member");
        tenantWide.UserId.Should().BeNull("neither addresses a single member");
    }

    /// <summary>
    /// A title key no other test can collide with, in the shape the list's search can be pointed at.
    /// </summary>
    /// <returns>The key.</returns>
    private static string NewTitleKey() => $"test.title.tenancy.{Guid.NewGuid()}";

    /// <summary>
    /// Reads back the notification a title key names, across every tenant because the row carries the
    /// tenant it belongs to and the lookup here is about the row rather than about a caller's reach.
    /// </summary>
    /// <param name="titleKey">The title key the test gave the notification.</param>
    /// <returns>The stored notification.</returns>
    private async Task<Notification> NotificationByTitleAsync(string titleKey)
        => await DbContext.Notifications
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(notification => notification.TitleKey == titleKey, TestContext.Current.CancellationToken);
}
