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

    /// <summary>
    /// How many notification hub connections one account may hold at once on one API instance - a browser
    /// holds one per open tab. A connection over the cap is refused and logged. Counted per instance, not
    /// across the deployment. Must be greater than zero.
    /// </summary>
    public int MaxConnectionsPerUser { get; set; } = 20;
}
