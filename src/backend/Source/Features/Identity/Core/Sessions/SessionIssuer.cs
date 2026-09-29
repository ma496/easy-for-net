namespace Backend.Features.Identity.Core.Sessions;

using System.Security.Cryptography;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Mints a session: reads what the account may do in the tenant it is about to act in, stores that as a
/// <see cref="SessionRecord"/>, and hands the record back so the caller can mint a token that names it.
/// Sign-in, refresh and tenant switch or exit all mint sessions, and all of them go through here.
/// </summary>
public interface ISessionIssuer
{
    /// <summary>
    /// Creates and stores a session for an account acting in one tenant.
    /// </summary>
    /// <param name="user">The account the session belongs to.</param>
    /// <param name="tenantId">The tenant the session acts in, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Token used to cancel the reads and the write.</param>
    /// <returns>The stored session.</returns>
    /// <exception cref="SessionStoreUnavailableException">The session could not be stored.</exception>
    Task<SessionRecord> IssueAsync(User user, Guid? tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ISessionIssuer"/>. The grants are read exactly as a token used to be minted with
/// them - <see cref="SessionGrants.ReadAsync"/>, for the tenant being acted in and no other - and the
/// session lives for the refresh-token lifetime, the longest a session can be renewed for.
/// </summary>
[NoDirectUse]
public sealed class SessionIssuer(AppDbContext dbContext,
                                  IPermissionFeatureFilter permissionFeatureFilter,
                                  ISessionStore sessionStore,
                                  IOptions<AuthSetting> authSetting) : ISessionIssuer
{
    /// <inheritdoc />
    public async Task<SessionRecord> IssueAsync(User user, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var grants = await SessionGrants.ReadAsync(dbContext, permissionFeatureFilter, user.Id, tenantId, user.IsPlatform, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var session = new SessionRecord
        {
            SessionId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsPlatform = user.IsPlatform,
            TenantId = tenantId,
            Roles = grants.Roles,
            Permissions = grants.Permissions,
            CreatedAt = now,
            ExpiresAt = now.AddHours(authSetting.Value.RefreshTokenValidity),
        };

        await sessionStore.CreateAsync(session, cancellationToken);

        return session;
    }
}
