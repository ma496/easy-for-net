namespace Backend.Features.Notifications.Core.Push;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// One live connection to the notification hub on this API instance.
/// </summary>
/// <param name="ConnectionId">SignalR's identifier of the connection.</param>
/// <param name="UserId">The account the connection authenticated as.</param>
/// <param name="TenantId">The tenant its session acts in, or <see langword="null"/> for platform scope.</param>
/// <param name="SessionId">The session its credential names, or <see langword="null"/> when it names none.</param>
/// <param name="Context">The hub's own handle on the connection, through which it can be closed.</param>
public sealed record NotificationConnection(
    string ConnectionId,
    Guid UserId,
    Guid? TenantId,
    string? SessionId,
    HubCallerContext Context);

/// <summary>
/// The notification hub connections open on this API instance, by connection, by account and by session.
/// It is a per-process singleton and knows nothing of other instances: the per-account cap it enforces is
/// a cap per instance, and a session's connections on another instance are that instance's to find.
/// </summary>
/// <remarks>
/// Reservation and release take one lock, so two connections of one account opening at once cannot both
/// take the last place. Connects and disconnects are rare next to messages, so the lock costs nothing that
/// matters. Looking connections up by session (<see cref="OfSession"/>) is what closing a session's
/// connections when the session ends needs.
/// </remarks>
public sealed class NotificationConnectionRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, NotificationConnection> _byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, HashSet<string>> _byUser = [];
    private readonly Dictionary<string, HashSet<string>> _bySession = new(StringComparer.Ordinal);

    /// <summary>
    /// Records a connection unless its account already holds <paramref name="maxPerUser"/> on this instance.
    /// </summary>
    /// <param name="connection">The connection to record.</param>
    /// <param name="maxPerUser">The most connections one account may hold on this instance.</param>
    /// <returns><see langword="true"/> when it was recorded; <see langword="false"/> when the account is at its cap.</returns>
    public bool TryAdd(NotificationConnection connection, int maxPerUser)
    {
        lock (_gate)
        {
            if (!_byUser.TryGetValue(connection.UserId, out var ofUser))
            {
                ofUser = new HashSet<string>(StringComparer.Ordinal);
                _byUser[connection.UserId] = ofUser;
            }

            if (ofUser.Count >= maxPerUser)
            {
                if (ofUser.Count == 0)
                {
                    _byUser.Remove(connection.UserId);
                }

                return false;
            }

            ofUser.Add(connection.ConnectionId);
            _byConnection[connection.ConnectionId] = connection;

            if (connection.SessionId is { } sessionId)
            {
                if (!_bySession.TryGetValue(sessionId, out var ofSession))
                {
                    ofSession = new HashSet<string>(StringComparer.Ordinal);
                    _bySession[sessionId] = ofSession;
                }

                ofSession.Add(connection.ConnectionId);
            }

            return true;
        }
    }

    /// <summary>
    /// Forgets a connection. Forgetting one that was never recorded - refused at the cap - does nothing.
    /// </summary>
    /// <param name="connectionId">The connection to forget.</param>
    public void Remove(string connectionId)
    {
        lock (_gate)
        {
            if (!_byConnection.Remove(connectionId, out var connection))
            {
                return;
            }

            if (_byUser.TryGetValue(connection.UserId, out var ofUser) && ofUser.Remove(connectionId) && ofUser.Count == 0)
            {
                _byUser.Remove(connection.UserId);
            }

            if (connection.SessionId is { } sessionId &&
                _bySession.TryGetValue(sessionId, out var ofSession) &&
                ofSession.Remove(connectionId) &&
                ofSession.Count == 0)
            {
                _bySession.Remove(sessionId);
            }
        }
    }

    /// <summary>
    /// The number of connections one account holds on this instance.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>Its connection count here.</returns>
    public int CountOf(Guid userId)
    {
        lock (_gate)
        {
            return _byUser.TryGetValue(userId, out var ofUser) ? ofUser.Count : 0;
        }
    }

    /// <summary>
    /// The connections on this instance whose credential names one session.
    /// </summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>A snapshot of its connections here; empty when it has none.</returns>
    public IReadOnlyList<NotificationConnection> OfSession(string sessionId)
    {
        lock (_gate)
        {
            return _bySession.TryGetValue(sessionId, out var ofSession)
                ? [.. ofSession.Select(connectionId => _byConnection[connectionId])]
                : [];
        }
    }
}
