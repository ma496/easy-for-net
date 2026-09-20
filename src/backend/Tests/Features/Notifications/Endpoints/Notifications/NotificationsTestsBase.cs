namespace Backend.Tests.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Base class for notification endpoint tests: the three ways a notification can be addressed, arranged
/// directly through the fixture's <see cref="AppTestsBase.DbContext"/> - to a single member of a tenant,
/// to every member of one, and to every user of the platform.
/// </summary>
/// <remarks>
/// <para>
/// Every row is written inside the scope that attributes it - a tenant for the two tenant-scoped
/// addressing modes, platform scope for the platform-wide one - which is the arrangement standing in
/// for the scope a request would have opened. Attribution is taken from the active scope at save time,
/// so a row naming no tenant cannot be written at all with no scope established, and a platform-wide
/// notification written while a tenant is active would quietly become that tenant's.
/// </para>
/// <para>
/// The tenant the tenant-scoped helpers default to is the bootstrap tenant, where every seeded account
/// holds its single membership: a notification there is one a seeded caller sees while acting in the
/// tenant sign-in resolves for it, which is what keeps the tests that came before the tenancy work
/// reading as they did.
/// </para>
/// </remarks>
// One collection for the whole notifications suite. A platform-wide notice reaches every account
// by definition, so a test that reads an unread count as a delta is measuring something any
// concurrently running notification test can change under it.
[Collection("Notifications")]
public abstract class NotificationsTestsBase(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Creates a notification addressed to a single member of a tenant (AC-053). The recipient is not
    /// checked against the tenant: the helper arranges a row, and the tests that need a recipient whose
    /// membership matches the attribution make one.
    /// </summary>
    /// <param name="userId">Identifier of the recipient.</param>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="tenantId">The tenant the notification belongs to, or <see langword="null"/> for the bootstrap tenant.</param>
    /// <returns>The arranged notification, carrying its assigned identity and attribution.</returns>
    protected async Task<Notification> CreateUserNotificationAsync(Guid userId, NotificationType type = NotificationType.Info, Guid? tenantId = null)
        => await ArrangeNotificationAsync(userId, type, tenantId ?? TestTenants.BootstrapTenantId, "user");

    /// <summary>
    /// Creates a notification addressed to every member of a tenant (AC-053). Its read state is per user
    /// and lives in a visit row rather than in the notification's own read flag.
    /// </summary>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="tenantId">The tenant the notification belongs to, or <see langword="null"/> for the bootstrap tenant.</param>
    /// <returns>The arranged notification, carrying its assigned identity and attribution.</returns>
    protected async Task<Notification> CreateTenantNotificationAsync(NotificationType type = NotificationType.Info, Guid? tenantId = null)
        => await ArrangeNotificationAsync(null, type, tenantId ?? TestTenants.BootstrapTenantId, "tenant");

    /// <summary>
    /// Creates a notification addressed to every user of the platform (AC-054). It names no tenant, which
    /// is exactly what tells it apart from a tenant-wide one.
    /// </summary>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <returns>The arranged notification, carrying its assigned identity and no attribution.</returns>
    protected async Task<Notification> CreateGlobalNotificationAsync(NotificationType type = NotificationType.Info)
        => await ArrangeNotificationAsync(null, type, tenantId: null, "global");

    /// <summary>
    /// Records a visit for a notification by a specific user, used for testing global notification read tracking.
    /// </summary>
    /// <param name="notificationId">The notification being visited.</param>
    /// <param name="userId">The user visiting it.</param>
    protected async Task MarkNotificationVisitedAsync(Guid notificationId, Guid userId)
    {
        var visit = new NotificationVisit
        {
            NotificationId = notificationId,
            UserId = userId,
            VisitedAt = DateTime.UtcNow
        };
        DbContext.NotificationVisits.Add(visit);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads a notification back across every tenant, because what is asserted of it is a row that a
    /// caller acting in another tenant must have been unable to reach.
    /// </summary>
    /// <param name="notificationId">The notification to read.</param>
    /// <returns>The stored notification as the database holds it, not as the change tracker last saw it.</returns>
    protected async Task<Notification> StoredNotificationAsync(Guid notificationId)
        => await DbContext.Notifications
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(notification => notification.Id == notificationId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Whether a user has visited a notification - which is where the read state of a notification
    /// addressed to an audience lives, the notification's own read flag being the one for a single
    /// recipient.
    /// </summary>
    /// <param name="notificationId">The notification whose read state is wanted.</param>
    /// <param name="userId">The user whose read state is wanted.</param>
    /// <returns><see langword="true"/> when that user has visited the notification.</returns>
    protected async Task<bool> IsVisitedAsync(Guid notificationId, Guid userId)
        => await DbContext.NotificationVisits
            .AsNoTracking()
            .AnyAsync(
                visit => visit.NotificationId == notificationId && visit.UserId == userId,
                TestContext.Current.CancellationToken);

    /// <summary>
    /// The notifications the list answers with for a term, read through a caller's own client so that the
    /// tenant being acted in is that caller's.
    /// </summary>
    /// <remarks>
    /// The term is the title key of a notification arranged or raised by the test, which carries a fresh
    /// identifier, so a match can only be that row: what is looked for is one specific notification's
    /// visibility rather than a count of whatever else the list happens to hold.
    /// </remarks>
    /// <param name="client">The client presenting the caller's token.</param>
    /// <param name="searchTerm">The term to search the list for.</param>
    /// <returns>The identifiers of the notifications it answered with.</returns>
    protected static async Task<List<Guid>> SearchIdsAsync(HttpClient client, string searchTerm)
    {
        var (rsp, page) = await client
            .GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(new()
            {
                Page = 1,
                All = true,
                Search = searchTerm
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        return [.. page.Items.Select(item => item.Id)];
    }

    /// <summary>
    /// Writes a notification in the scope that attributes it: the tenant named, or platform scope when
    /// there is none, which is the only way a row that names no tenant is ever written.
    /// </summary>
    /// <param name="userId">The single recipient, or <see langword="null"/> when the notification is addressed to an audience.</param>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="tenantId">The tenant the notification belongs to, or <see langword="null"/> for a platform-wide one.</param>
    /// <param name="keyPrefix">Names the addressing mode in the title and message keys, so a failure names what was arranged.</param>
    /// <returns>The arranged notification.</returns>
    private async Task<Notification> ArrangeNotificationAsync(Guid? userId, NotificationType type, Guid? tenantId, string keyPrefix)
    {
        var notification = new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            TitleKey = $"test.title.{keyPrefix}.{Guid.NewGuid()}",
            MessageKey = $"test.message.{keyPrefix}.{Guid.NewGuid()}",
            IsRead = false
        };

        using var scope = tenantId is { } activeTenantId
            ? TenantContext.BeginTenant(activeTenantId)
            : TenantContext.BeginPlatformScope();

        DbContext.Notifications.Add(notification);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return notification;
    }
}
