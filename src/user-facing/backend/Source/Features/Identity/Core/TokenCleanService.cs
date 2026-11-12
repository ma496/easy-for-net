namespace Backend.Features.Identity.Core;

/// <summary>
/// Maintenance service that removes expired single-use tokens (e.g., email-verification, password-reset tokens) from the store.
/// </summary>
public interface ITokenCleanService
{
    Task DeleteExpiredTokensAsync();
}

/// <summary>
/// Default <see cref="ITokenCleanService"/> implementation that bulk-deletes single-use tokens whose expiry has passed.
/// </summary>
public class TokenCleanService(AppDbContext dbContext) : ITokenCleanService
{
    public async Task DeleteExpiredTokensAsync()
    {
        await dbContext.Tokens.Where(t => t.Expiry < DateTime.UtcNow).ExecuteDeleteAsync();
    }
}