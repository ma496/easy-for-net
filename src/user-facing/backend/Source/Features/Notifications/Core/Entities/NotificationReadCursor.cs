using Backend.ShareData.Entities.Base;

namespace Backend.Features.Notifications.Core.Entities;

/// <summary>
/// A user's "mark all as read" point for the all-users broadcasts. A broadcast (one with no
/// <see cref="Notification.UserId"/>) that has no <see cref="NotificationVisit"/> row for the user is
/// read when its CreatedAt is at or before the user's cursor. A user holds at most one cursor.
/// </summary>
public class NotificationReadCursor : BaseEntity<Guid>
{
    public Guid UserId { get; set; }

    // Every broadcast created at or before this instant, and without a visit row for the user, counts
    // as read.
    public DateTime ReadAllAt { get; set; }
}
