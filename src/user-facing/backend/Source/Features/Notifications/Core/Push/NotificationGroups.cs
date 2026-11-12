namespace Backend.Features.Notifications.Core.Push;

/// <summary>
/// The names of the hub groups a notification is published to, one per addressing mode. They are the
/// same two audiences <see cref="NotificationQueries.VisibleTo"/> reads a caller's rows from, so a
/// connection joined to the groups its session names receives exactly the rows that session would list.
/// </summary>
/// <remarks>
/// A connection is joined to its groups by <see cref="NotificationHub"/> from the projected session alone;
/// the client never names a group, and nothing a request carries besides its session reaches these names.
/// </remarks>
public static class NotificationGroups
{
    /// <summary>
    /// The group every connection joins: the all-users broadcast.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// The group of one account: every connection it holds, on every device.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The group name, <c>u:{userId}</c>.</returns>
    public static string User(Guid userId) => $"u:{userId:D}";
}
