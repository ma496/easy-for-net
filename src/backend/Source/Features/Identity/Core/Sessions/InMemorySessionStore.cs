namespace Backend.Features.Identity.Core.Sessions;

using System.Collections.Concurrent;

/// <summary>
/// A process-local <see cref="ISessionStore"/> with the same semantics as the Redis one - expiry,
/// per-user and per-tenant indexes, indexes pruned as they are read - so a host that must not depend on
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
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _byTenant = new();

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
    {
        if (session.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Task.CompletedTask;
        }

        _sessions[session.SessionId] = session;
        Index(_byUser, session.UserId, session.SessionId);
        if (session.TenantId is { } tenantId)
        {
            Index(_byTenant, tenantId, session.SessionId);
        }

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
    public Task RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Revoke(_byUser, userId, _ => true);

    /// <inheritdoc />
    public Task RevokeByUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => Revoke(_byUser, userId, session => session.TenantId == tenantId);

    /// <inheritdoc />
    public Task RevokeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Revoke(_byTenant, tenantId, _ => true);

    /// <summary>
    /// How many entries the user's index holds, live or not. Exposed so a test can prove an expired
    /// session's entry is dropped when the index is read.
    /// </summary>
    /// <param name="userId">The account whose index is counted.</param>
    /// <returns>The number of session identifiers indexed for the account.</returns>
    internal int IndexedForUser(Guid userId)
        => _byUser.TryGetValue(userId, out var members) ? members.Count : 0;

    /// <summary>
    /// How many entries the tenant's index holds, live or not.
    /// </summary>
    /// <param name="tenantId">The tenant whose index is counted.</param>
    /// <returns>The number of session identifiers indexed for the tenant.</returns>
    internal int IndexedForTenant(Guid tenantId)
        => _byTenant.TryGetValue(tenantId, out var members) ? members.Count : 0;

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
    /// Deletes the live sessions of one index that match, and drops from that index every entry that
    /// names no live session - the expired ones a Redis index would also have outlived.
    /// </summary>
    private Task Revoke(ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index,
                        Guid key,
                        Func<SessionRecord, bool> matches)
    {
        if (!index.TryGetValue(key, out var members))
        {
            return Task.CompletedTask;
        }

        foreach (var sessionId in members.Keys)
        {
            var session = Live(sessionId);
            if (session is null)
            {
                members.TryRemove(sessionId, out _);
            }
            else if (matches(session) && _sessions.TryRemove(sessionId, out var removed))
            {
                Unindex(removed);
            }
        }

        return Task.CompletedTask;
    }

    private static void Index(ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index, Guid key, string sessionId)
        => index.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[sessionId] = 0;

    private void Unindex(SessionRecord session)
    {
        if (_byUser.TryGetValue(session.UserId, out var byUser))
        {
            byUser.TryRemove(session.SessionId, out _);
        }

        if (session.TenantId is { } tenantId && _byTenant.TryGetValue(tenantId, out var byTenant))
        {
            byTenant.TryRemove(session.SessionId, out _);
        }
    }
}
