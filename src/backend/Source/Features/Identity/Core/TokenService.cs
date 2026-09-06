namespace Backend.Features.Identity.Core;

using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Manages short-lived, single-use tokens (email verification, password reset, etc.) for <see cref="User"/> accounts.
/// </summary>
public interface ITokenService
{
    Task<Token> GenerateTokenAsync(User user, TokenPurpose purpose);
    bool ValidateToken(Token token);
    Task<bool> UseTokenAsync(Token token, CancellationToken cancellationToken = default);
    Task<Token?> GetTokenAsync(string token, TokenPurpose purpose, CancellationToken cancellationToken = default);
    Task DeleteTokenAsync(Token token);
}

/// <summary>
/// EF Core implementation of <see cref="ITokenService"/> that issues, validates, consumes, and deletes single-use tokens.
/// </summary>
[NoDirectUse]
public class TokenService(AppDbContext dbContext) : ITokenService
{
    public async Task<Token> GenerateTokenAsync(User user, TokenPurpose purpose)
    {
        var rawValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var token = new Token
        {
            Value = HashToken(rawValue),
            Expiry = DateTime.UtcNow.AddMinutes(15),
            UserId = user.Id,
            Purpose = purpose
        };
        dbContext.Tokens.Add(token);
        await dbContext.SaveChangesAsync();
        return new Token
        {
            Id = token.Id,
            Value = rawValue,
            Expiry = token.Expiry,
            UserId = token.UserId,
            Purpose = token.Purpose
        };
    }

    public bool ValidateToken(Token token)
    {
        return !token.IsUsed && token.Expiry > DateTime.UtcNow;
    }

    public async Task<bool> UseTokenAsync(Token token, CancellationToken cancellationToken = default)
    {
        var updated = await dbContext.Tokens
            .Where(item => item.Id == token.Id && !item.IsUsed && item.Expiry > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsUsed, true), cancellationToken);
        return updated == 1;
    }

    public async Task<Token?> GetTokenAsync(string token, TokenPurpose purpose, CancellationToken cancellationToken = default)
    {
        return await dbContext.Tokens
            .FirstOrDefaultAsync(t => t.Value == HashToken(token) && t.Purpose == purpose, cancellationToken);
    }

    public async Task DeleteTokenAsync(Token token)
    {
        dbContext.Tokens.Remove(token);
        await dbContext.SaveChangesAsync();
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
