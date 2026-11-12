namespace Backend.Features.Notifications.Core;

/// <summary>
/// Categorizes the visual/behavioral severity of a <see cref="Entities.Notification"/>,
/// allowing the UI to render it with the appropriate styling and icon.
/// </summary>
[AllowOutside]
public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}
