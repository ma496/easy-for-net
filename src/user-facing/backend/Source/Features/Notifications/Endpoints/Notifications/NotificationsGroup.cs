namespace Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// This group that registers all notification-related endpoints under the "notifications" route prefix.
/// </summary>
public sealed class NotificationsGroup : Group
{
    public NotificationsGroup()
    {
        Configure("notifications", ep => { });
    }
}
