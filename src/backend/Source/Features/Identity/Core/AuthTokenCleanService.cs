namespace Backend.Features.Identity.Core;

/// <summary>
/// Maintenance service that purges expired refresh tokens from the AuthToken store.
/// </summary>
public interface IAuthTokenCleanService
{
    Task DeleteExpiredTokensAsync();
}

/// <summary>
/// Default <see cref="IAuthTokenCleanService"/> implementation that bulk-deletes auth tokens whose
/// refresh-token expiry has passed.
/// </summary>
/// <remarks>
/// The delete spans every tenant, and has to: it runs as a recurring job rather than on a request, so
/// there is no scope for it to inherit, and an expired session is rubbish whichever tenant it was
/// established for. Relaxing the restriction by name leaves the soft-delete filter in force.
/// </remarks>
public class AuthTokenCleanService(AppDbContext dbContext) : IAuthTokenCleanService
{
    public async Task DeleteExpiredTokensAsync()
    {
        await dbContext.AuthTokens.AcrossAllTenants().Where(at => at.RefreshExpiry < DateTime.UtcNow).ExecuteDeleteAsync();
    }
}