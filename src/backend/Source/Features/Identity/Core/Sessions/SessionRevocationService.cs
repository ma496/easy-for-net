namespace Backend.Features.Identity.Core.Sessions;

using Backend.Attributes;

/// <summary>
/// Ends live sessions when something they were minted from has changed: the account, its password, the
/// roles it holds or what those roles grant, or the tenant it acts in. Each call deletes the matching
/// sessions from the session store, so their access tokens answer 401 on the very next request, together
/// with the refresh-token rows that belong to them, so none of them can be renewed either.
/// </summary>
/// <remarks>
/// <para>
/// This is the one contract another feature may name to end a session; nothing outside this feature
/// touches <see cref="ISessionStore"/> or the refresh-token rows directly. Every member speaks in
/// <see cref="Guid"/> and <see cref="string"/>, so no type of this feature leaks through it.
/// </para>
/// <para>
/// A scope is a tenant, or <see langword="null"/> for platform scope - a session acting in no tenant.
/// A session is in a scope when it acts in it, or when its refresh-token row records it (a row keeps the
/// tenant a session was established for even after a renewal dropped it, and the next renewal would
/// enter it again).
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
    /// <summary>Ends every session of one account, in every tenant and in platform scope.</summary>
    /// <param name="userId">The account whose sessions are ended.</param>
    /// <param name="cancellationToken">Token used to cancel the deletes.</param>
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Ends the sessions of one account in one scope, leaving its sessions elsewhere alone.</summary>
    /// <param name="userId">The account whose sessions are ended.</param>
    /// <param name="tenantId">The tenant, or <see langword="null"/> for platform scope.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the deletes.</param>
    Task RevokeUserInScopeAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Ends every session acting in one tenant, whoever holds it.</summary>
    /// <param name="tenantId">The tenant whose sessions are ended.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the deletes.</param>
    Task RevokeTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Ends the sessions of several accounts in one scope - the holders of a role, say.</summary>
    /// <param name="userIds">The accounts whose sessions are ended.</param>
    /// <param name="tenantId">The tenant, or <see langword="null"/> for platform scope.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the deletes.</param>
    Task RevokeUsersInScopeAsync(IReadOnlyCollection<Guid> userIds, Guid? tenantId, CancellationToken cancellationToken = default);

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
    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureNoOpenTransaction();

        await Rows().Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await sessionStore.RevokeByUserAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task RevokeUserInScopeAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
        => RevokeUsersInScopeAsync([userId], tenantId, cancellationToken);

    /// <inheritdoc />
    public async Task RevokeTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureNoOpenTransaction();

        var sessionIds = await Rows()
            .Where(row => row.TenantId == tenantId && row.SessionId != null)
            .Select(row => row.SessionId!)
            .ToListAsync(cancellationToken);

        await Rows().Where(row => row.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await DeleteSessionsAsync(sessionIds, cancellationToken);
        await sessionStore.RevokeByTenantAsync(tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RevokeUsersInScopeAsync(IReadOnlyCollection<Guid> userIds, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        EnsureNoOpenTransaction();

        var accounts = userIds.Distinct().ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        // Rows first, and the store not at all until they are gone: a row recording the scope is deleted
        // outright, remembering the session it names, so a store outage past this point still leaves
        // nothing able to renew what was revoked.
        var rows = await Rows()
            .Where(row => accounts.Contains(row.UserId))
            .Select(row => new { row.Id, row.SessionId, row.TenantId })
            .ToListAsync(cancellationToken);

        var revokedSessionIds = rows
            .Where(row => row.TenantId == tenantId && row.SessionId is not null)
            .Select(row => row.SessionId!)
            .ToList();
        await Rows().Where(row => accounts.Contains(row.UserId) && row.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);

        // A row recording another scope is in this one when its session acts here - the tenant a renewal
        // dropped stays on the row, the session is what says where it acts now. Only these ask the store.
        var revokedRowIds = new List<Guid>();
        foreach (var row in rows.Where(row => row.TenantId != tenantId && row.SessionId is not null))
        {
            var session = await sessionStore.GetAsync(row.SessionId!, cancellationToken);
            if (session is null || session.TenantId != tenantId)
            {
                continue;
            }

            revokedRowIds.Add(row.Id);
            revokedSessionIds.Add(row.SessionId!);
        }

        if (revokedRowIds.Count > 0)
        {
            await Rows().Where(row => revokedRowIds.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        await DeleteSessionsAsync(revokedSessionIds, cancellationToken);

        // The store's own tenant index reaches a session in that tenant whatever its row says; platform
        // scope has no index, and is covered by the rows above.
        if (tenantId is { } scopeTenantId)
        {
            foreach (var userId in accounts)
            {
                await sessionStore.RevokeByUserInTenantAsync(userId, scopeTenantId, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task RevokeUserExceptAsync(Guid userId, string keptSessionId, CancellationToken cancellationToken = default)
    {
        EnsureNoOpenTransaction();

        // A row naming no session predates the column and is revoked with the rest: it is not the kept one.
        var others = Rows().Where(row => row.UserId == userId && (row.SessionId == null || row.SessionId != keptSessionId));
        var sessionIds = await others
            .Where(row => row.SessionId != null)
            .Select(row => row.SessionId!)
            .ToListAsync(cancellationToken);

        await others.ExecuteDeleteAsync(cancellationToken);
        await DeleteSessionsAsync(sessionIds, cancellationToken);
    }

    /// <summary>
    /// Every refresh-token row, whatever tenant it names: revocation reaches sessions in tenants other than
    /// the one the request acts in, so tenant restriction is relaxed by name.
    /// </summary>
    private IQueryable<Entities.AuthToken> Rows() => dbContext.AuthTokens.AcrossAllTenants();

    private async Task DeleteSessionsAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken)
    {
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
