namespace Backend.Features.Notifications.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Represents a notification together with the audience it is addressed to. <see cref="TenantId"/> and
/// <see cref="UserId"/> combine into four addressing modes: both set means a single member of that tenant,
/// a tenant with no user means every member of that tenant, a user with no tenant means that one account
/// while it acts in platform scope, and both null means every user of the platform, whichever scope they
/// act in. A personal notification - tenant or platform - is shown only in the scope it was raised in, so
/// a platform-scope one never follows its recipient into a tenant; only the platform-wide broadcast is seen
/// everywhere. Notifications are auditable and support soft deletion.
/// </summary>
public class Notification : AuditableEntity<Guid>, ISoftDelete, IMayHaveTenant
{
    public NotificationType Type { get; set; }
    public string TitleKey { get; set; } = null!;
    public string MessageKey { get; set; } = null!;
    // It is only applied when UserId is not null,
    // it tracks the user specific read state of the notification,
    // for a notification addressed to an audience rather than to one user - tenant-wide or
    // platform-wide - this field is ignored and the Visits collection should be used instead.
    public bool IsRead { get; set; }
    public string? Group { get; set; }
    public string? Metadata { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // The tenant the notification belongs to. Null means platform scope: combined with a null UserId
    // it is the platform-wide notification every user sees, whichever tenant they are acting in, and
    // combined with a UserId it is a personal notification seen only while acting in platform scope.
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }

    // For a notification addressed to an audience - tenant-wide or platform-wide - we track the
    // visits (reads) in a separate table; for user-specific notifications, the IsRead field is
    // sufficient and the Visits collection is not used.
    public ICollection<NotificationVisit> Visits { get; set; } = [];
}
