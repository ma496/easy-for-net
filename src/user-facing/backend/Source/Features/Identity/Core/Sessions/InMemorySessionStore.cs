using System.Collections.Concurrent;

namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// A process-local <see cref="ISessionStore"/> with the same semantics as the Redis one - expiry,
/// a per-user index pruned as it is read - so a host that must not depend on
/// Redis (the test host) behaves the way a deployment does. It is registered as a singleton, because
/// every scope has to see the sessions every other scope minted.
/// </summary>
/// <remarks>
/// It is registered only under the <c>Testing</c> environment (see <see cref="SessionStoreRegistration"/>).
/// Sessions do not survive the process and are not shared between instances of it, which is exactly
/// what a deployment cannot accept.
/// </remarks>
/// <param name="timeProvider">The clock expiry is judged by; the system clock when none is supplied.</param>
[NoDirectUse]
public sealed class InMemorySessionStore(TimeProvider? timeProvider = null) : ISessionStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _byUser = new();

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
    {
        if (session.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Task.CompletedTask;
        }

        _sessions[session.SessionId] = session;
        Index(_byUser, session.UserId, session.SessionId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Live(sessionId));

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(sessionId, out var removed))
        {
            Unindex(removed);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Revoke(_byUser, userId);

    /// <summary>
    /// How many entries the user's index holds, live or not. Exposed so a test can prove an expired
    /// session's entry is dropped when the index is read.
    /// </summary>
    /// <param name="userId">The account whose index is counted.</param>
    /// <returns>The number of session identifiers indexed for the account.</returns>
    internal int IndexedForUser(Guid userId)
        => _byUser.TryGetValue(userId, out var members) ? members.Count : 0;

    private SessionRecord? Live(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        if (session.ExpiresAt > _timeProvider.GetUtcNow())
        {
            return session;
        }

        // Expired: gone for every purpose, and removed here so it costs nothing further.
        if (_sessions.TryRemove(sessionId, out var expired))
        {
            Unindex(expired);
        }

        return null;
    }

    /// <summary>
    /// Deletes the live sessions of one index, and drops from that index every entry that
    /// names no live session - the expired ones a Redis index would also have outlived.
    /// </summary>
    /// <returns>The identifiers of the sessions deleted.</returns>
    private Task<IReadOnlyList<string>> Revoke(ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index,
                        Guid key)
    {
        var revoked = new List<string>();
        if (!index.TryGetValue(key, out var members))
        {
            return Task.FromResult<IReadOnlyList<string>>(revoked);
        }

        foreach (var sessionId in members.Keys)
        {
            var session = Live(sessionId);
            if (session is null)
            {
                members.TryRemove(sessionId, out _);
            }
            else if (_sessions.TryRemove(sessionId, out var removed))
            {
                Unindex(removed);
                revoked.Add(sessionId);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(revoked);
    }

    private static void Index(ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index, Guid key, string sessionId)
        => index.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[sessionId] = 0;

    private void Unindex(SessionRecord session)
    {
        if (_byUser.TryGetValue(session.UserId, out var byUser))
        {
            byUser.TryRemove(session.SessionId, out _);
        }
    }
}
