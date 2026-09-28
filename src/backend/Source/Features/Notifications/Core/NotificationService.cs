namespace Backend.Features.Notifications.Core;

using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Application service for creating and querying notifications. A notification is addressed to a single
/// account in the active scope, to every member of the active tenant, or to every user of the platform,
/// and each addressing mode has its own method here. Attribution is taken from the active tenant scope and
/// never from a caller-supplied identifier, so a personal notification is shown only to its recipient while
/// acting in the scope it was raised in - the tenant, or platform scope - while a platform-wide one stays
/// distinguishable from a tenant-wide one because it names no tenant at all.
/// </summary>
/// <remarks>
/// Published to other slices so that they can raise notifications about their own events; the entities
/// behind it stay private to this slice.
/// </remarks>
[AllowOutside]
public interface INotificationService
{
    /// <summary>
    /// Returns the number of unread notifications visible to the given user in the active scope: their own
    /// unread notifications raised in that scope, the notifications addressed to the whole tenant they have
    /// not visited, and the platform-wide notifications they have not visited. Personal notifications
    /// raised in the user's other tenants, or in platform scope while they act in a tenant, are not counted.
    /// </summary>
    /// <param name="userId">Identifier of the user whose unread notifications are counted.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The total number of unread notifications for the user in the active scope.</returns>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification addressed to a single account in the active scope. Raised inside a tenant it
    /// is attributed to that tenant and the recipient sees it only while acting in it; raised in platform
    /// scope it names no tenant and the recipient sees it only while acting in platform scope, which is how
    /// a platform account is told about something personally rather than through a platform-wide broadcast.
    /// </summary>
    /// <param name="userId">Identifier of the recipient user.</param>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="titleKey">Localization key used to render the notification title.</param>
    /// <param name="messageKey">Localization key used to render the notification body.</param>
    /// <param name="group">Optional logical grouping used to filter notifications in the UI.</param>
    /// <param name="metadata">Optional opaque metadata payload (typically JSON).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    Task NewUserNotificationAsync(Guid userId, NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification addressed to every member of the active tenant. Read state for it is per
    /// user and is tracked through notification visits rather than through the row's read flag.
    /// </summary>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="titleKey">Localization key used to render the notification title.</param>
    /// <param name="messageKey">Localization key used to render the notification body.</param>
    /// <param name="group">Optional logical grouping used to filter notifications in the UI.</param>
    /// <param name="metadata">Optional opaque metadata payload (typically JSON).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    /// <exception cref="InvalidOperationException">The active scope is platform scope rather than a tenant.</exception>
    Task NewTenantNotificationAsync(NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification addressed to every user of the platform. It is attributed to no tenant, so
    /// it stays visible whichever tenant its recipients act in, and it is told apart from a tenant-wide
    /// notification by exactly that: it names no tenant.
    /// </summary>
    /// <param name="type">Visual/severity category of the notification.</param>
    /// <param name="titleKey">Localization key used to render the notification title.</param>
    /// <param name="messageKey">Localization key used to render the notification body.</param>
    /// <param name="group">Optional logical grouping used to filter notifications in the UI.</param>
    /// <param name="metadata">Optional opaque metadata payload (typically JSON).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NewGlobalNotificationAsync(NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default EF Core-backed implementation of <see cref="INotificationService"/>. Every write here takes
/// its tenant attribution from <see cref="ITenantContext"/>: a personal notification belongs to whichever
/// scope is active, the tenant-wide one requires an actual tenant to be active, and the platform-wide one
/// deliberately writes its row in platform scope so that it belongs to no tenant.
/// </summary>
public class NotificationService(AppDbContext dbContext, ITenantContext tenantContext) : INotificationService
{
    /// <summary>
    /// Counts the unread notifications the user can see in the active scope. The set is the one
    /// <see cref="NotificationQueries.VisibleTo"/> defines, read over an <c>AcrossAllTenants()</c> source
    /// because the tenant query filter on its own would drop the platform-wide rows; each row is then
    /// unread by its own rule - the row's flag for a personal notification, a missing visit for an
    /// audience one.
    /// </summary>
    /// <param name="userId">Identifier of the user whose unread notifications are counted.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The total number of unread notifications for the user in the active scope.</returns>
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Reading the active tenant here rather than leaning on the query filter makes the scope
        // requirement explicit: with no scope established this throws instead of counting rows the
        // caller is not acting for.
        var activeTenantId = tenantContext.CurrentTenantId;

        var visitedNotificationIds = await dbContext.NotificationVisits
            .Where(v => v.UserId == userId)
            .Select(v => v.NotificationId)
            .ToListAsync(cancellationToken);

        var newCount = await dbContext.Notifications
            .AcrossAllTenants()
            .VisibleTo(userId, activeTenantId)
            .CountAsync(x =>
                (x.UserId == userId && !x.IsRead) ||
                (x.UserId == null && !visitedNotificationIds.Contains(x.Id)),
                cancellationToken);
        return newCount;
    }

    /// <summary>
    /// Persists a notification addressed to one account in the active scope, attributed to the active
    /// tenant or, in platform scope, to no tenant - so the scope it is shown in is the scope it was
    /// raised in.
    /// </summary>
    public async Task NewUserNotificationAsync(Guid userId, NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default)
    {
        // Read explicitly rather than left to the save-time stamping alone, so that with no scope
        // established this throws instead of writing a row that no scope can see.
        var notification = new Notification
        {
            TenantId = tenantContext.CurrentTenantId,
            UserId = userId,
            Type = type,
            TitleKey = titleKey,
            MessageKey = messageKey,
            Group = group,
            Metadata = metadata
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Persists a notification addressed to every member of the active tenant.
    /// </summary>
    public async Task NewTenantNotificationAsync(NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            TenantId = RequireActiveTenantId(),
            UserId = null,
            Type = type,
            TitleKey = titleKey,
            MessageKey = messageKey,
            Group = group,
            Metadata = metadata
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Persists a notification addressed to every user of the platform. The row has to be written in
    /// platform scope: a tenant-scoped row saved while a tenant is active is stamped with that tenant,
    /// which would quietly turn the broadcast into a tenant-wide notification.
    /// </summary>
    /// <remarks>
    /// The scope covers the save and not only the add, because attribution is stamped at save time.
    /// A caller must therefore not be holding unsaved additions of other tenant-scoped rows in the
    /// same <see cref="AppDbContext"/> when it calls this: those rows would be flushed here in
    /// platform scope and stamped with no tenant. Save that work before raising the broadcast.
    /// </remarks>
    public async Task NewGlobalNotificationAsync(NotificationType type, string titleKey, string messageKey, string? group = null, string? metadata = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            TenantId = null,
            UserId = null,
            Type = type,
            TitleKey = titleKey,
            MessageKey = messageKey,
            Group = group,
            Metadata = metadata
        };

        using var platformScope = tenantContext.BeginPlatformScope();

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the tenant a tenant-wide notification is attributed to. Platform scope is refused here
    /// rather than read as "no tenant": a row naming neither a tenant nor a user is the platform-wide
    /// broadcast, so a tenant-wide notification raised in platform scope would quietly reach every user
    /// of the platform. Callers that mean the platform-wide audience use
    /// <see cref="NewGlobalNotificationAsync"/> instead.
    /// </summary>
    /// <returns>The identifier of the tenant currently being acted for.</returns>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    /// <exception cref="InvalidOperationException">The active scope is platform scope rather than a tenant.</exception>
    private Guid RequireActiveTenantId()
        => tenantContext.CurrentTenantId
           ?? throw new InvalidOperationException(
               "A tenant-wide notification must be raised while acting in a tenant. Use the platform-wide notification method to address every user of the platform.");
}