namespace Backend.Features.Identity.Core;

using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Persists issued JWT (access/refresh) token pairs to the database and validates subsequent refresh-token requests.
/// Each stored pair records the tenant its session acts in, so that refreshing a session re-establishes that very
/// tenant rather than dropping it or carrying a previously active one.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>
    /// Stores an issued token pair for later refresh validation.
    /// </summary>
    /// <param name="rsp">The token pair that is about to be sent to the client.</param>
    /// <param name="tenantId">
    /// The tenant the session acts in, or <see langword="null"/> when it acts in none - the state of a user
    /// who holds no active membership, or who holds several and has not chosen between them. Every issuance
    /// point states this explicitly, so that a session can never inherit whatever tenant happened to be
    /// recorded last.
    /// </param>
    /// <returns>The stored record.</returns>
    Task<AuthToken> SaveTokenAsync(TokenResponse rsp, Guid? tenantId);

    /// <summary>
    /// Validates a refresh request against the stored pairs and consumes the matching one, so that a refresh
    /// token is usable exactly once.
    /// </summary>
    /// <param name="req">The incoming refresh request, carrying the user identifier and the refresh token.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the delete.</param>
    /// <returns>
    /// Whether a live token was consumed and, when one was, the tenant its session was acting in. The tenant is
    /// returned here because the row that carries it is deleted by this call, and it is the only record of which
    /// tenant the expiring session was established for.
    /// </returns>
    Task<RefreshTokenConsumption> ConsumeRefreshTokenAsync(TokenRequest req, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every stored token pair of one user, ending all of their sessions.
    /// </summary>
    /// <param name="userId">The user whose tokens are revoked.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the one stored pair a refresh token belongs to, ending that session and no other. This
    /// is how a session that is being replaced in place - the tenant it acts in has changed, so a new
    /// pair is issued for the same signed-in account - stops the pair it replaces from being refreshed
    /// afterwards, while the account's sessions on other devices carry on untouched.
    /// </summary>
    /// <param name="userId">The account the token belongs to.</param>
    /// <param name="refreshToken">The refresh token whose stored pair is revoked.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <remarks>
    /// A token that names no stored pair - already consumed, already expired and cleaned, or never
    /// issued - is not an error: there is simply nothing left to revoke.
    /// </remarks>
    Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of consuming a refresh token: whether a live, unexpired, not-yet-used token was found and
/// deleted, and the tenant the session it belonged to was acting in. A consumed token with no tenant is a
/// legitimate outcome and is deliberately distinguishable from a rejected one, so that a caller never reads
/// "no tenant" as "not valid" nor a rejection as a session with no tenant.
/// </summary>
public sealed class RefreshTokenConsumption
{
    /// <summary>
    /// The outcome of a request whose token was unknown, malformed, expired or already used. It carries no
    /// tenant, because no session was re-established.
    /// </summary>
    public static RefreshTokenConsumption Rejected { get; } = new();

    /// <summary>
    /// Gets a value indicating whether a live token was found and consumed by this call. Only the call that
    /// deletes the row sees <see langword="true"/>, so a replayed token is refused even when two requests
    /// carry it at the same moment.
    /// </summary>
    public bool Consumed { get; init; }

    /// <summary>
    /// Gets the tenant the consumed session was acting in, or <see langword="null"/> when it acted in none.
    /// Meaningful only while <see cref="Consumed"/> is <see langword="true"/>.
    /// </summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
/// EF Core implementation of <see cref="IAuthTokenService"/> that stores token records and checks refresh-token validity.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AuthToken"/> is tenant-scoped like every other persisted kind, but this service is the
/// code that establishes and changes a session's tenant, so it is never acting inside the tenant a
/// row names while it touches that row. Both halves of that are handled here rather than being
/// exempted in <see cref="AppDbContext"/>, and both are deliberate and named.
/// </para>
/// <para>
/// Every read relaxes tenant restriction by name. Consuming a refresh token happens on an anonymous
/// request, which establishes no scope at all, and revoking an account's sessions has to reach the
/// ones it holds in other tenants - signing out ends every session, not the sessions of whichever
/// tenant the request happened to arrive in. The soft-delete filter stays in force throughout.
/// </para>
/// <para>
/// The one write opens the scope of the tenant the session is for, so the row is attributed exactly
/// the way every other tenant-scoped write is instead of inheriting the request's own tenant. That
/// matters in three places: a sign-in and a refresh are anonymous and have no scope to inherit, a
/// switch issues the session for the tenant being entered while still acting in the one being left,
/// and an exit issues a session for no tenant while still acting in the one being left.
/// </para>
/// </remarks>
[NoDirectUse]
public class AuthTokenService(AppDbContext dbContext, ITenantContext tenantContext) : IAuthTokenService
{
    public async Task<AuthToken> SaveTokenAsync(TokenResponse rsp, Guid? tenantId)
    {
        var authToken = new AuthToken
        {
            AccessToken = HashToken(rsp.AccessToken),
            AccessExpiry = rsp.AccessExpiry,
            RefreshToken = HashToken(rsp.RefreshToken),
            RefreshExpiry = rsp.RefreshExpiry,
            TenantId = tenantId,
            UserId = Guid.Parse(rsp.UserId),
        };

        // The session's own tenant is established for the write and nothing else, so the attribution
        // rules see a row that agrees with the active scope rather than one that contradicts it or one
        // written with no scope at all. The scope is restored the moment the row is persisted.
        using (tenantId is { } sessionTenantId
            ? tenantContext.BeginTenant(sessionTenantId)
            : tenantContext.BeginPlatformScope())
        {
            await dbContext.AuthTokens.AddAsync(authToken);
            await dbContext.SaveChangesAsync();
        }

        return authToken;
    }

    public async Task<RefreshTokenConsumption> ConsumeRefreshTokenAsync(TokenRequest req, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(req.UserId, out var userId) || string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            return RefreshTokenConsumption.Rejected;
        }

        var refreshTokenHash = HashToken(req.RefreshToken);

        // The row is read before it is deleted because a delete cannot hand back the columns it removed, and
        // the tenant on this row is the only surviving record of which tenant the expiring session acted in.
        var issued = await dbContext.AuthTokens
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(at => at.UserId == userId && at.RefreshToken == refreshTokenHash && at.RefreshExpiry > DateTime.UtcNow)
            .Select(at => new { at.Id, at.TenantId })
            .FirstOrDefaultAsync(cancellationToken);
        if (issued is null)
        {
            return RefreshTokenConsumption.Rejected;
        }

        // Deleting by identity and demanding that exactly one row went is what keeps a refresh token
        // single-use: two requests replaying the same token both read it, and only the one whose delete
        // actually removed the row is allowed to renew the session.
        var deleted = await dbContext.AuthTokens
            .AcrossAllTenants()
            .Where(at => at.Id == issued.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 1
            ? new RefreshTokenConsumption { Consumed = true, TenantId = issued.TenantId }
            : RefreshTokenConsumption.Rejected;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await dbContext.AuthTokens
            .AcrossAllTenants()
            .Where(token => token.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        // Matched on the stored hash, exactly as consuming one is: the token itself is never stored, so
        // the row can only be found by hashing what the caller presented.
        var refreshTokenHash = HashToken(refreshToken);

        await dbContext.AuthTokens
            .AcrossAllTenants()
            .Where(token => token.UserId == userId && token.RefreshToken == refreshTokenHash)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
