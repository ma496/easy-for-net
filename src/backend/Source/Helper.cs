namespace Backend;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Application-wide static helpers used during startup to wire up feature
/// modules and to build the claim set issued to authenticated users.
/// </summary>
public static class Helper
{
    /// <summary>
    /// Discovers every type implementing <see cref="IFeature"/> in the current
    /// assembly and invokes its static <c>AddServices</c> method to register
    /// its services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application's configuration manager, forwarded to each feature.</param>
    public static void AddFeatures(IServiceCollection services, ConfigurationManager configuration)
    {
        var features = typeof(Helper).Assembly.GetTypes()
            .Where(p => typeof(IFeature).IsAssignableFrom(p) && !p.IsAbstract)
            .ToList();

        foreach (var feature in features)
        {
            feature.GetMethod("AddServices")?.Invoke(null, [services, configuration]);
        }
    }

    /// <summary>
    /// Builds the <see cref="Claim"/> list for an authenticated user, including
    /// identity, email, role, and permission claims derived from the supplied
    /// user, roles, and permissions.
    /// </summary>
    /// <param name="user">The authenticated user to source identity claims from.</param>
    /// <param name="roles">The role names to emit as <see cref="ClaimTypes.Role"/> claims.</param>
    /// <param name="permissions">The permission names to emit as application permission claims.</param>
    /// <returns>The list of claims representing the user's identity, roles, and permissions.</returns>
    public static List<Claim> CreateClaims(User user, List<string> roles, List<string> permissions)
    {
        var claims = new List<Claim>
        {
            new (ClaimTypes.NameIdentifier, user.Id.ToString()),
            new (ClaimTypes.Name, user.Username),
            new (ClaimTypes.Email, user.Email),
            new (ClaimConstants.SessionVersion, CreateSessionVersion(user.PasswordHash)),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(ClaimConstants.Permission, p)));
        return claims;
    }

    /// <summary>
    /// Creates a non-sensitive version identifier that changes whenever the password hash changes.
    /// </summary>
    public static string CreateSessionVersion(string passwordHash)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
    }
}
