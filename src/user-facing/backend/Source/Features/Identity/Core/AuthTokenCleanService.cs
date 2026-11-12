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
public class AuthTokenCleanService(AppDbContext dbContext) : IAuthTokenCleanService
{
    public async Task DeleteExpiredTokensAsync()
    {
        await dbContext.AuthTokens.Where(at => at.RefreshExpiry < DateTime.UtcNow).ExecuteDeleteAsync();
    }
}