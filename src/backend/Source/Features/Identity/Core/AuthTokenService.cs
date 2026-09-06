namespace Backend.Features.Identity.Core;

using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Persists issued JWT (access/refresh) token pairs to the database and validates subsequent refresh-token requests.
/// </summary>
public interface IAuthTokenService
{
    Task<AuthToken> SaveTokenAsync(TokenResponse rsp);
    Task<bool> ConsumeRefreshTokenAsync(TokenRequest req, CancellationToken cancellationToken = default);
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core implementation of <see cref="IAuthTokenService"/> that stores token records and checks refresh-token validity.
/// </summary>
[NoDirectUse]
public class AuthTokenService(AppDbContext dbContext) : IAuthTokenService
{
    public async Task<AuthToken> SaveTokenAsync(TokenResponse rsp)
    {
        var authToken = new AuthToken
        {
            AccessToken = HashToken(rsp.AccessToken),
            AccessExpiry = rsp.AccessExpiry,
            RefreshToken = HashToken(rsp.RefreshToken),
            RefreshExpiry = rsp.RefreshExpiry,
            UserId = Guid.Parse(rsp.UserId),
        };
        await dbContext.AuthTokens.AddAsync(authToken);
        await dbContext.SaveChangesAsync();
        return authToken;
    }

    public async Task<bool> ConsumeRefreshTokenAsync(TokenRequest req, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(req.UserId, out var userId) || string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            return false;
        }

        var refreshTokenHash = HashToken(req.RefreshToken);
        var deleted = await dbContext.AuthTokens
            .Where(at => at.UserId == userId && at.RefreshToken == refreshTokenHash && at.RefreshExpiry > DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await dbContext.AuthTokens
            .Where(token => token.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
