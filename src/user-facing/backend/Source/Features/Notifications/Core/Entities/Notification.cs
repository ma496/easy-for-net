using Backend.ShareData.Entities.Base;

namespace Backend.Features.Notifications.Core.Entities;

/// <summary>
/// Represents a notification together with the audience it is addressed to: a <see cref="UserId"/>
/// names one account, and a null one addresses every user - the all-users broadcast. Notifications are
/// auditable and support soft deletion.
/// </summary>
public class Notification : AuditableEntity<Guid>, ISoftDelete
{
    public NotificationType Type { get; set; }
    public string TitleKey { get; set; } = null!;
    public string MessageKey { get; set; } = null!;
    // It is only applied when UserId is not null,
    // it tracks the user specific read state of the notification,
    // for a broadcast addressed to every user this field is ignored and the Visits collection should be
    // used instead.
    public bool IsRead { get; set; }
    public string? Group { get; set; }
    public string? Metadata { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // The recipient. Null means the notification is addressed to every user.
    public Guid? UserId { get; set; }

    // For a broadcast we track the visits (reads) in a separate table; for user-specific notifications,
    // the IsRead field is sufficient and the Visits collection is not used.
    public ICollection<NotificationVisit> Visits { get; set; } = [];
}
