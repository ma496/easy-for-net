namespace Backend.Tests.Fakes;

using System.Collections.Concurrent;
using Backend.Exceptions;
using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Which sessions and accounts the test host's session store should behave as if it could not reach its
/// backing store for. It is a singleton, and a test names only sessions and accounts it created itself,
/// so a store that is "down" for one test is up for every other one running beside it.
/// </summary>
public sealed class SessionStoreFaults
{
    private readonly ConcurrentDictionary<string, byte> _unreachableSessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, byte> _unwritableUsers = new();

    /// <summary>Makes every read and delete of one session fail as an outage.</summary>
    /// <param name="sessionId">The session to make unreachable.</param>
    public void MakeUnreachable(string sessionId) => _unreachableSessions[sessionId] = 0;

    /// <summary>Makes every attempt to create a session for one account fail as an outage.</summary>
    /// <param name="userId">The account whose new sessions cannot be stored.</param>
    public void MakeUnwritable(Guid userId) => _unwritableUsers[userId] = 0;

    internal bool IsUnreachable(string sessionId) => _unreachableSessions.ContainsKey(sessionId);

    internal bool IsUnwritable(Guid userId) => _unwritableUsers.ContainsKey(userId);
}

/// <summary>
/// Wraps the real (in-memory) store of the test host and throws
/// <see cref="SessionStoreUnavailableException"/> - exactly what the Redis store throws when Redis is
/// down - for the sessions and accounts <see cref="SessionStoreFaults"/> names, passing everything else
/// straight through.
/// </summary>
/// <param name="inner">The store the host would have used.</param>
/// <param name="faults">What to fail for.</param>
public sealed class FaultInjectingSessionStore(ISessionStore inner, SessionStoreFaults faults) : ISessionStore
{
    /// <summary>Gets the store being wrapped.</summary>
    public ISessionStore Inner => inner;

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
        => faults.IsUnwritable(session.UserId) ? throw Outage() : inner.CreateAsync(session, cancellationToken);

    /// <inheritdoc />
    public Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => faults.IsUnreachable(sessionId) ? throw Outage() : inner.GetAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        => faults.IsUnreachable(sessionId) ? throw Outage() : inner.DeleteAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => inner.RevokeByUserAsync(userId, cancellationToken);

    /// <inheritdoc />
    public Task RevokeByUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => inner.RevokeByUserInTenantAsync(userId, tenantId, cancellationToken);

    /// <inheritdoc />
    public Task RevokeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => inner.RevokeByTenantAsync(tenantId, cancellationToken);

    private static SessionStoreUnavailableException Outage()
        => new("The session store could not be reached (injected by the test host).");
}
