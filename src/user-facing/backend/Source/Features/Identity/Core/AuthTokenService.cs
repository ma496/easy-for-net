using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Core.Sessions;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Features.Identity.Core;

/// <summary>
/// Persists issued JWT (access/refresh) token pairs to the database and validates subsequent refresh-token requests.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>
    /// Stores an issued token pair for later refresh validation.
    /// </summary>
    /// <param name="rsp">The token pair that is about to be sent to the client.</param>
    /// <param name="sessionId">
    /// The session-store record the pair belongs to, or <see langword="null"/> for a pair with none. It is
    /// written on the row so that a refresh can delete the session it replaces.
    /// </param>
    /// <param name="securityStamp">
    /// The account's security stamp the pair was authorized under. A refresh carries it forward and is
    /// refused once the account's stamp has moved on, which is what ends a chain across a password change.
    /// </param>
    /// <returns>The stored record.</returns>
    Task<AuthToken> SaveTokenAsync(TokenResponse rsp, string? sessionId, Guid securityStamp);

    /// <summary>
    /// Validates a refresh request against the stored pairs and consumes the matching one, so that a refresh
    /// token is usable exactly once.
    /// </summary>
    /// <param name="req">The incoming refresh request, carrying the user identifier and the refresh token.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the delete.</param>
    /// <returns>
    /// Whether a live token was consumed and, when one was, the session and security stamp it carried.
    /// </returns>
    Task<RefreshTokenConsumption> ConsumeRefreshTokenAsync(TokenRequest req, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the stored pairs of one session, ending it and no other: the account's sessions on other
    /// hosts and devices carry on. This is what signing out of one host does.
    /// </summary>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="sessionId">The session whose pairs are revoked.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    Task RevokeSessionAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the one stored pair a refresh token belongs to, ending that session and no other. This
    /// is how a session that is being replaced in place stops the pair it replaces from being refreshed
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

    /// <summary>
    /// Moves one session onto the account's new security stamp - its stored pairs and its session-store
    /// record both - so the session a caller changed its own password from goes on renewing while every other chain of the account is refused.
    /// </summary>
    /// <remarks>
    /// The record is rewritten only when a pair of the session was still stored, and is deleted again when
    /// that pair is gone once the rewrite has landed. Every revocation, sign-out and renewal deletes a
    /// session's pairs before its record, so one that ran concurrently either deletes the record after it
    /// was rewritten or is seen by that re-check - a session ended in the meantime is never brought back.
    /// </remarks>
    /// <param name="userId">The account the session belongs to.</param>
    /// <param name="sessionId">The session that is restamped.</param>
    /// <param name="securityStamp">The account's new stamp.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    Task RestampSessionAsync(Guid userId, string sessionId, Guid securityStamp, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of consuming a refresh token: whether a live, unexpired, not-yet-used token was found and
/// deleted, and what the session it belonged to carried.
/// </summary>
public sealed class RefreshTokenConsumption
{
    /// <summary>
    /// The outcome of a request whose token was unknown, malformed, expired or already used.
    /// </summary>
    public static RefreshTokenConsumption Rejected { get; } = new();

    /// <summary>
    /// Gets a value indicating whether a live token was found and consumed by this call. Only the call that
    /// deletes the row sees <see langword="true"/>, so a replayed token is refused even when two requests
    /// carry it at the same moment.
    /// </summary>
    public bool Consumed { get; init; }

    /// <summary>
    /// Gets the identifier of the session-store record the consumed pair belonged to, or
    /// <see langword="null"/> when the row named none. A refresh deletes that session as it mints its
    /// replacement. Meaningful only while <see cref="Consumed"/> is <see langword="true"/>.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the account security stamp the consumed pair was issued under. A renewal is refused when it no
    /// longer matches the account's. Meaningful only while <see cref="Consumed"/> is <see langword="true"/>.
    /// </summary>
    public Guid SecurityStamp { get; init; }
}

/// <summary>
/// EF Core implementation of <see cref="IAuthTokenService"/> that stores token records and checks refresh-token validity.
/// </summary>
[NoDirectUse]
public class AuthTokenService(AppDbContext dbContext, ISessionStore sessionStore) : IAuthTokenService
{
    public async Task<AuthToken> SaveTokenAsync(TokenResponse rsp, string? sessionId, Guid securityStamp)
    {
        var authToken = new AuthToken
        {
            AccessToken = HashToken(rsp.AccessToken),
            AccessExpiry = rsp.AccessExpiry,
            RefreshToken = HashToken(rsp.RefreshToken),
            RefreshExpiry = rsp.RefreshExpiry,
            SessionId = sessionId,
            SecurityStamp = securityStamp,
            UserId = Guid.Parse(rsp.UserId),
        };

        await dbContext.AuthTokens.AddAsync(authToken);
        await dbContext.SaveChangesAsync();

        return authToken;
    }

    public async Task<RefreshTokenConsumption> ConsumeRefreshTokenAsync(TokenRequest req, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(req.UserId, out var userId) || string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            return RefreshTokenConsumption.Rejected;
        }

        var refreshTokenHash = HashToken(req.RefreshToken);

        // The row is read before it is deleted because a delete cannot hand back the columns it removed.
        var issued = await dbContext.AuthTokens
            .AsNoTracking()
            .Where(at => at.UserId == userId && at.RefreshToken == refreshTokenHash && at.RefreshExpiry > DateTime.UtcNow)
            .Select(at => new { at.Id, at.SessionId, at.SecurityStamp })
            .FirstOrDefaultAsync(cancellationToken);
        if (issued is null)
        {
            return RefreshTokenConsumption.Rejected;
        }

        // Deleting by identity and demanding that exactly one row went is what keeps a refresh token
        // single-use: two requests replaying the same token both read it, and only the one whose delete
        // actually removed the row is allowed to renew the session.
        var deleted = await dbContext.AuthTokens
            .Where(at => at.Id == issued.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 1
            ? new RefreshTokenConsumption { Consumed = true, SessionId = issued.SessionId, SecurityStamp = issued.SecurityStamp }
            : RefreshTokenConsumption.Rejected;
    }

    public async Task RevokeSessionAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default)
    {
        await dbContext.AuthTokens
            .Where(token => token.UserId == userId && token.SessionId == sessionId)
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
            .Where(token => token.UserId == userId && token.RefreshToken == refreshTokenHash)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RestampSessionAsync(Guid userId, string sessionId, Guid securityStamp, CancellationToken cancellationToken = default)
    {
        var restamped = await dbContext.AuthTokens
            .Where(token => token.UserId == userId && token.SessionId == sessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.SecurityStamp, securityStamp), cancellationToken);
        if (restamped == 0
            || await sessionStore.GetAsync(sessionId, cancellationToken) is not { } session
            || session.UserId != userId)
        {
            return;
        }

        // Storing a record under an existing identifier replaces it, so this rewrites the one field and
        // keeps the session's identity, grants and expiry exactly as they were.
        await sessionStore.CreateAsync(new SessionRecord
        {
            SessionId = session.SessionId,
            UserId = session.UserId,
            Username = session.Username,
            Email = session.Email,
            Roles = session.Roles,
            Permissions = session.Permissions,
            SecurityStamp = securityStamp,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
        }, cancellationToken);

        var stillIssued = await dbContext.AuthTokens
            .AsNoTracking()
            .AnyAsync(token => token.UserId == userId && token.SessionId == sessionId, cancellationToken);
        if (!stillIssued)
        {
            await sessionStore.DeleteAsync(sessionId, cancellationToken);
        }
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
