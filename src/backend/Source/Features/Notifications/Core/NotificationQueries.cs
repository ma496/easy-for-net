namespace Backend.Features.Notifications.Core;

using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// The one definition of which notifications a caller can see in the scope they act in, so that every
/// surface answering about a notification - reading one, listing them, counting them unread, marking them
/// - answers about the same set.
/// </summary>
/// <remarks>
/// Each of those surfaces has to relax the tenant query filter by name and narrow back down by hand,
/// because the filter alone would hide every platform-wide notification from a caller acting in a tenant:
/// those name no tenant, and a filter comparing the row's tenant to the active one can never match them.
/// Leaving that relaxation to each surface to spell out is how one of them ends up hiding the
/// platform-wide notifications the list still shows, so it is spelled out once, here.
/// </remarks>
internal static class NotificationQueries
{
    /// <summary>
    /// The notifications one user can see in the scope named: the notifications raised in that scope -
    /// the tenant, or platform scope - addressed to them personally or to every member of it, and the
    /// platform-wide notifications, which stay visible in every scope. A personal notification raised in
    /// another scope is not among them, so a platform-scope one does not follow its recipient into a
    /// tenant, and one raised in a tenant is seen nowhere else.
    /// </summary>
    /// <param name="notifications">The notifications source, with the tenant filter already relaxed by <c>AcrossAllTenants()</c>.</param>
    /// <param name="userId">Identifier of the user whose notifications are wanted.</param>
    /// <param name="activeTenantId">The tenant being acted in, or <see langword="null"/> for platform scope.</param>
    /// <returns>The query narrowed to the notifications visible to that user in that scope.</returns>
    internal static IQueryable<Notification> VisibleTo(this IQueryable<Notification> notifications, Guid userId, Guid? activeTenantId)
        => notifications.Where(notification =>
            (notification.TenantId == activeTenantId && (notification.UserId == userId || notification.UserId == null)) ||
            (notification.TenantId == null && notification.UserId == null));
}
