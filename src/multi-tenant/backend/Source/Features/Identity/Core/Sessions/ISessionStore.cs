namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Where session records live between the request that mints them and the requests they authorize.
/// Every authenticated request reads it, so an implementation is a shared, fast key-value store and
/// never the relational database.
/// </summary>
/// <remarks>
/// Besides the records, a store keeps two secondary indexes - the sessions of each user and of each
/// tenant - so a whole group can be revoked without scanning. An index entry that outlives its record
/// (the record expired) is harmless: it is tolerated, and dropped the next time the index is read.
/// An implementation that cannot reach its backing store throws
/// <see cref="SessionStoreUnavailableException"/> and nothing else, which is what lets the caller fail
/// closed rather than guess.
/// </remarks>
public interface ISessionStore
{
    /// <summary>Stores a session until its <see cref="SessionRecord.ExpiresAt"/> and indexes it.</summary>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default);

    /// <summary>Reads one session.</summary>
    /// <param name="sessionId">The identifier the token named.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The session, or <see langword="null"/> when it is unknown, deleted or expired.</returns>
    Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Deletes one session, if it exists.</summary>
    /// <param name="sessionId">The session to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Deletes every session of one account, in every tenant.</summary>
    /// <param name="userId">The account whose sessions are revoked.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <returns>The identifiers of the sessions it deleted.</returns>
    Task<IReadOnlyList<string>> RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the sessions of one account that act in one tenant, leaving its others alone.</summary>
    /// <param name="userId">The account whose sessions are revoked.</param>
    /// <param name="tenantId">The tenant the revoked sessions act in.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <returns>The identifiers of the sessions it deleted.</returns>
    Task<IReadOnlyList<string>> RevokeByUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Deletes every session acting in one tenant, whoever holds it.</summary>
    /// <param name="tenantId">The tenant whose sessions are revoked.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <returns>The identifiers of the sessions it deleted.</returns>
    Task<IReadOnlyList<string>> RevokeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
