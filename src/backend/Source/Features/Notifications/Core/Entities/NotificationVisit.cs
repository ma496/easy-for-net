namespace Backend.Features.Notifications.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// A user's explicit read state for one audience notification - tenant-wide or platform-wide - since
/// the read flag on <see cref="Notification"/> only applies to user-targeted notifications. A visit row
/// overrides the user's <see cref="NotificationReadCursor"/> either way: <see cref="IsRead"/> true marks
/// the notification read even when it is newer than the cursor, and false marks it unread even when the
/// cursor covers it. The tenant is derived through <see cref="Notification"/>.
/// </summary>
public class NotificationVisit : BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public DateTime VisitedAt { get; set; }
    public bool IsRead { get; set; }

    public Guid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;
}
