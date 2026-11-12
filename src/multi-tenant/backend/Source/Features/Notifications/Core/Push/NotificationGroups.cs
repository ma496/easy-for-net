namespace Backend.Features.Notifications.Core.Push;

/// <summary>
/// The names of the hub groups a notification is published to, one per addressing mode. They are the
/// same three audiences <see cref="NotificationQueries.VisibleTo"/> reads a caller's rows from, so a
/// connection joined to the groups its session names receives exactly the rows that session would list.
/// </summary>
/// <remarks>
/// A connection is joined to its groups by <see cref="NotificationHub"/> from the projected session alone;
/// the client never names a group, and nothing a request carries besides its session reaches these names.
/// </remarks>
public static class NotificationGroups
{
    /// <summary>
    /// The group every connection joins: the platform-wide broadcast, which is visible in every scope.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// The group of one account acting in one scope. A personal notification raised in a tenant is visible
    /// only while its recipient acts in that tenant, and one raised in platform scope only while they act in
    /// platform scope, so the scope is part of the name - the same account's connection acting elsewhere is
    /// in a different group and does not receive it.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="tenantId">The tenant it acts in, or <see langword="null"/> for platform scope.</param>
    /// <returns>The group name, <c>u:{userId}:{tenantId|platform}</c>.</returns>
    public static string User(Guid userId, Guid? tenantId)
        => $"u:{userId:D}:{(tenantId is { } id ? id.ToString("D") : "platform")}";

    /// <summary>
    /// The group of every connection acting in one tenant: the tenant-wide notification's audience.
    /// </summary>
    /// <param name="tenantId">The tenant.</param>
    /// <returns>The group name, <c>t:{tenantId}</c>.</returns>
    public static string Tenant(Guid tenantId) => $"t:{tenantId:D}";
}
