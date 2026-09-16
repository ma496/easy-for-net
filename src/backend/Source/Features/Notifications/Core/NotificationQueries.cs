namespace Backend.Features.Notifications.Core;

using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// The one definition of which notifications a caller can see while acting in a tenant, so that every
/// surface answering about a notification - reading one, listing them, counting them unread, marking them
/// - answers about the same set.
/// </summary>
/// <remarks>
/// Each of those surfaces has to relax the tenant query filter by name and narrow back down by hand,
/// because the filter alone would hide every platform-wide notification: those name no tenant, and a
/// filter comparing the row's tenant to the active one can never match them. Leaving that relaxation to
/// each surface to spell out is how one of them ends up hiding the platform-wide notifications the list
/// still shows, so it is spelled out once, here.
/// </remarks>
internal static class NotificationQueries
{
    /// <summary>
    /// The notifications one user can see while acting in the tenant named: the notifications raised in
    /// that tenant addressed to them personally, the notifications addressed to every member of that
    /// tenant, and the platform-wide notifications, which stay visible whichever tenant the caller acts
    /// in. Notifications raised in the caller's other tenants are not among them.
    /// </summary>
    /// <param name="notifications">The notifications source, with the tenant filter already relaxed by <c>AcrossAllTenants()</c>.</param>
    /// <param name="userId">Identifier of the user whose notifications are wanted.</param>
    /// <param name="activeTenantId">The tenant being acted in, or <see langword="null"/> for platform scope.</param>
    /// <returns>The query narrowed to the notifications visible to that user in that tenant.</returns>
    internal static IQueryable<Notification> VisibleTo(this IQueryable<Notification> notifications, Guid userId, Guid? activeTenantId)
        => notifications.Where(notification =>
            (notification.TenantId == activeTenantId || notification.TenantId == null) &&
            (notification.UserId == userId || notification.UserId == null));
}
