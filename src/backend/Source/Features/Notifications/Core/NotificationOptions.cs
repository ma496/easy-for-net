namespace Backend.Features.Notifications.Core;

/// <summary>
/// Strongly-typed configuration options for the notifications subsystem, bound from the
/// <c>Notifications</c> configuration section and validated at startup.
/// </summary>
public class NotificationOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "Notifications";

    /// <summary>
    /// How many days a notification is kept before the daily retention job hard-deletes it, read or unread,
    /// soft-deleted or not. Must be greater than zero.
    /// </summary>
    public int RetentionDays { get; set; } = 90;
}
