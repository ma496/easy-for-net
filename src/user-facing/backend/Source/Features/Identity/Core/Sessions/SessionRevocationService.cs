using Backend.Attributes;

namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Ends live sessions when something they were minted from has changed: the account, its password, the
/// roles it holds or what those roles grant. Each call deletes the matching sessions from the session
/// store, so their access tokens answer 401 on the very next request, together with the refresh-token
/// rows that belong to them, so none of them can be renewed either.
/// </summary>
/// <remarks>
/// <para>
/// This is the one contract another feature may name to end a session; nothing outside this feature
/// touches <see cref="ISessionStore"/> or the refresh-token rows directly. Every member speaks in
/// <see cref="Guid"/> and <see cref="string"/>, so no type of this feature leaks through it.
/// </para>
/// <para>
/// Revocation must run after the change it answers has committed: a change that rolls back must leave
/// every session working. It is refused with <see cref="InvalidOperationException"/> while a transaction
/// is open on the request's context, rather than joining it and deleting refresh rows that a rollback
/// would restore while the sessions they belong to stay deleted.
/// </para>
/// </remarks>
[AllowOutside]
public interface ISessionRevocationService
{
    /// <summary>Ends every session of one account.</summary>
    /// <param name="userId">The account whose sessions are ended.</param>
    /// <param name="cancellationToken">Token used to cancel the deletes.</param>
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Ends every session of several accounts - the holders of a role, say.</summary>
    /// <param name="userIds">The accounts whose sessions are ended.</param>
    /// <param name="cancellationToken">Token used to cancel the deletes.</param>
    Task RevokeUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends every session of one account except one - the session a caller changing its own password is
    /// making the change from.
    /// </summary>
    /// <param name="userId">The account whose other sessions are ended.</param>
    /// <param name="keptSessionId">The session that survives.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the deletes.</param>
    /// <remarks>
    /// The sessions are found through the account's refresh-token rows, because the store can revoke an
    /// account's sessions only all at once. Every session is issued together with its row, so the only
    /// session this can miss is one whose renewal failed after consuming its row - and no token can renew
    /// that one, so it ends with its access token.
    /// </remarks>
    Task RevokeUserExceptAsync(Guid userId, string keptSessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ISessionRevocationService"/>. Refresh-token rows are deleted before the sessions, so
/// a session store outage part-way through still leaves nothing able to renew what was revoked.
/// </summary>
[NoDirectUse]
public sealed class SessionRevocationService(AppDbContext dbContext, ISessionStore sessionStore) : ISessionRevocationService
{
    /// <inheritdoc />
    public Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => RevokeUsersAsync([userId], cancellationToken);

    /// <inheritdoc />
    public async Task RevokeUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        EnsureNoOpenTransaction();

        var accounts = userIds.Distinct().ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        await dbContext.AuthTokens.Where(row => accounts.Contains(row.UserId)).ExecuteDeleteAsync(cancellationToken);
        foreach (var userId in accounts)
        {
            await sessionStore.RevokeByUserAsync(userId, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task RevokeUserExceptAsync(Guid userId, string keptSessionId, CancellationToken cancellationToken = default)
    {
        EnsureNoOpenTransaction();

        // A row naming no session predates the column and is revoked with the rest: it is not the kept one.
        var others = dbContext.AuthTokens.Where(row => row.UserId == userId && (row.SessionId == null || row.SessionId != keptSessionId));
        var sessionIds = await others
            .Where(row => row.SessionId != null)
            .Select(row => row.SessionId!)
            .ToListAsync(cancellationToken);

        await others.ExecuteDeleteAsync(cancellationToken);
        foreach (var sessionId in sessionIds.Distinct(StringComparer.Ordinal))
        {
            await sessionStore.DeleteAsync(sessionId, cancellationToken);
        }
    }

    private void EnsureNoOpenTransaction()
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "Sessions are revoked after the change that ends them has committed, so that a rollback revokes nothing. Commit the transaction first.");
        }
    }
}
