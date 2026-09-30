namespace Backend.Features.Notifications.Core.Push;

using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// The names of the client methods the notification hub invokes. The hub is server-to-client only, so
/// these are the whole of its protocol; the web app's hub client listens for exactly these names.
/// </summary>
public static class NotificationHubMethods
{
    /// <summary>A notification was committed for an audience the connection belongs to.</summary>
    public const string NotificationReceived = "notificationReceived";

    /// <summary>The caller's own read state changed; carries their recomputed, capped unread count.</summary>
    public const string UnreadCountChanged = "unreadCountChanged";
}

/// <summary>
/// The payload of <see cref="NotificationHubMethods.NotificationReceived"/>: the fields a row of the
/// notification list carries, so the web app can put it straight into the list it already shows. A
/// freshly raised notification is unread for everyone it reaches, so <see cref="IsRead"/> is always
/// <see langword="false"/> and no per-recipient read state is computed.
/// </summary>
public sealed class NotificationReceivedMessage
{
    /// <summary>The notification's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Visual/severity category, serialized by name as the list endpoint does.</summary>
    public NotificationType Type { get; init; }

    /// <summary>Localization key of the title.</summary>
    public string TitleKey { get; init; } = null!;

    /// <summary>Localization key of the message.</summary>
    public string MessageKey { get; init; } = null!;

    /// <summary>Optional group slug.</summary>
    public string? Group { get; init; }

    /// <summary>Optional opaque metadata (typically JSON).</summary>
    public string? Metadata { get; init; }

    /// <summary>When the notification was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Always <see langword="false"/>: nobody has read a notification that was just raised.</summary>
    public bool IsRead { get; init; }

    /// <summary>
    /// Builds the message for a saved notification. It must be called after the save, which is what stamps
    /// <see cref="CreatedAt"/>.
    /// </summary>
    /// <param name="notification">The saved notification.</param>
    /// <returns>The message published for it.</returns>
    internal static NotificationReceivedMessage From(Notification notification)
        => new()
        {
            Id = notification.Id,
            Type = notification.Type,
            TitleKey = notification.TitleKey,
            MessageKey = notification.MessageKey,
            Group = notification.Group,
            Metadata = notification.Metadata,
            CreatedAt = notification.CreatedAt,
            IsRead = false
        };
}

/// <summary>
/// The payload of <see cref="NotificationHubMethods.UnreadCountChanged"/>.
/// </summary>
public sealed class UnreadCountChangedMessage
{
    /// <summary>The caller's unread count in the scope the change was made in, capped at <see cref="NotificationQueries.UnreadCountCap"/>.</summary>
    public int Count { get; init; }
}
