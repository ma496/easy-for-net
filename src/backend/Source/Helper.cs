namespace Backend;

using System.Security.Claims;
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
    /// user, roles, and permissions, plus the tenant the session acts in when one is active.
    /// </summary>
    /// <param name="user">The authenticated user to source identity claims from.</param>
    /// <param name="roles">
    /// The role names to emit as <see cref="ClaimTypes.Role"/> claims. When <paramref name="tenantId"/>
    /// is supplied these must be the roles the user holds in that tenant, so that the session never
    /// carries authorization established for another tenant.
    /// </param>
    /// <param name="permissions">
    /// The permission names to emit as application permission claims. When <paramref name="tenantId"/>
    /// is supplied these must be the permissions the user's roles in that tenant grant.
    /// </param>
    /// <param name="tenantId">
    /// The tenant the session acts in, emitted as a single <see cref="ClaimConstants.TenantId"/> claim.
    /// Pass <see langword="null"/> when no tenant is active, in which case no tenant claim is issued
    /// and the session acts in the platform scope - which for an ordinary account is no authority at
    /// all, since its permissions are narrowed to that scope when they are read.
    /// </param>
    /// <returns>The list of claims representing the user's identity, active tenant, roles, and permissions.</returns>
    public static List<Claim> CreateClaims(User user, List<string> roles, List<string> permissions, Guid? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new (ClaimTypes.NameIdentifier, user.Id.ToString()),
            new (ClaimTypes.Name, user.Username),
            new (ClaimTypes.Email, user.Email),
        };
        if (user.IsPlatform)
            claims.Add(new (ClaimConstants.IsPlatform, bool.TrueString));
        if (tenantId.HasValue)
            claims.Add(new (ClaimConstants.TenantId, tenantId.Value.ToString()));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(ClaimConstants.Permission, p)));
        return claims;
    }
}
