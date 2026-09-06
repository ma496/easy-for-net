namespace Backend.Features.Identity.Core;

using System.Security.Claims;
using System.Security.Cryptography;

/// <summary>
/// Validates that an authenticated principal was issued for the user's current password.
/// </summary>
public static class SessionValidator
{
    public static async Task<bool> IsCurrentAsync(ClaimsPrincipal? principal, AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionVersion = principal?.FindFirstValue(ClaimConstants.SessionVersion);
        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(sessionVersion))
        {
            return false;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId && candidate.IsActive)
            .Select(candidate => new { candidate.PasswordHash })
            .FirstOrDefaultAsync(cancellationToken);

        try
        {
            return user is not null &&
                   CryptographicOperations.FixedTimeEquals(
                       Convert.FromBase64String(sessionVersion),
                       Convert.FromBase64String(Helper.CreateSessionVersion(user.PasswordHash)));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
