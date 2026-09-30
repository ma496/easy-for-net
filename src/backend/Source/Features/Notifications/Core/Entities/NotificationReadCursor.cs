namespace Backend.Features.Notifications.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// A user's "mark all as read" point for one audience. An audience notification (one with no
/// <see cref="Notification.UserId"/>) that has no <see cref="NotificationVisit"/> row for the user is
/// read when its CreatedAt is at or before the user's cursor for
/// its audience: the cursor naming the notification's tenant for a tenant-wide notification, and the
/// cursor with a null <see cref="TenantId"/> for a platform-wide one. A user holds at most one cursor
/// per audience, the platform-wide one included.
/// </summary>
public class NotificationReadCursor : BaseEntity<Guid>, IMayHaveTenant
{
    public Guid UserId { get; set; }

    // The audience this cursor covers: a tenant's own tenant-wide notifications, or - when null -
    // the platform-wide notifications every user sees.
    public Guid? TenantId { get; set; }

    // Every audience notification created at or before this instant, and without a visit row for
    // the user, counts as read.
    public DateTime ReadAllAt { get; set; }
}
