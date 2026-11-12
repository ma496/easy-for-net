using System.Security.Cryptography;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Mints a session: reads what the account may do, stores that as a <see cref="SessionRecord"/>, and
/// hands the record back so the caller can mint a token that names it. Sign-in and refresh both mint
/// sessions, and both go through here.
/// </summary>
public interface ISessionIssuer
{
    /// <summary>
    /// Creates and stores a session for an account.
    /// </summary>
    /// <param name="user">The account the session belongs to.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the write.</param>
    /// <returns>The stored session.</returns>
    /// <exception cref="SessionStoreUnavailableException">The session could not be stored.</exception>
    Task<SessionRecord> IssueAsync(User user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ISessionIssuer"/>. The grants are read with <see cref="SessionGrants.ReadAsync"/>
/// and the session lives for the refresh-token lifetime, the longest a session can be renewed for.
/// </summary>
[NoDirectUse]
public sealed class SessionIssuer(AppDbContext dbContext,
                                  ISessionStore sessionStore,
                                  IOptions<AuthSetting> authSetting) : ISessionIssuer
{
    /// <inheritdoc />
    public async Task<SessionRecord> IssueAsync(User user, CancellationToken cancellationToken = default)
    {
        var grants = await SessionGrants.ReadAsync(dbContext, user.Id, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var session = new SessionRecord
        {
            SessionId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Roles = grants.Roles,
            Permissions = grants.Permissions,
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = now.AddHours(authSetting.Value.RefreshTokenValidity),
        };

        await sessionStore.CreateAsync(session, cancellationToken);

        return session;
    }
}
