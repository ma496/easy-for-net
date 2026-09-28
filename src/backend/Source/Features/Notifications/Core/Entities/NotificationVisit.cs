namespace Backend.Features.Notifications.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Tracks per-user visits (i.e. read state) for global notifications. A visit row is created
/// when a user sees a broadcast notification, since the read flag on <see cref="Notification"/>
/// only applies to user-targeted notifications.
/// </summary>
public class NotificationVisit : BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public DateTime VisitedAt { get; set; }

    public Guid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;
}