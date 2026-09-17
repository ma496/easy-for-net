namespace Backend.Middleware;

using System.Security.Claims;
using Backend.Features.Identity.Core;

/// <summary>
/// Decides whether a request is exempt from the active tenant requirement. It is shared by the
/// processor that enforces the requirement and by the handler that explains authorization refusals,
/// so the two can never disagree about which requests need a tenant.
/// </summary>
internal static class TenantRequirement
{
    /// <summary>
    /// Reports whether the endpoint being called runs with no tenant established for this caller:
    /// every caller when it carries <see cref="AllowNoTenantAttribute"/>, and a caller holding
    /// <see cref="Allow.Platform_Administration"/> when it carries
    /// <see cref="AllowPlatformNoTenantAttribute"/>. A request whose endpoint cannot be identified is
    /// treated as not exempt, so an operation whose tenant rule cannot be established is refused
    /// rather than allowed to run unrestricted.
    /// </summary>
    /// <param name="httpContext">The request being handled.</param>
    /// <returns><see langword="true"/> when the endpoint runs with no tenant established.</returns>
    public static bool IsExempt(HttpContext httpContext)
    {
        var definition = httpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();
        if (definition is null)
        {
            return false;
        }

        if (definition.EndpointType.IsDefined(typeof(AllowNoTenantAttribute), inherit: false))
        {
            return true;
        }

        // The platform tier is read from the permission claims the session check has just recomputed,
        // never from the request, so naming a tenant or omitting one cannot earn the exemption.
        return definition.EndpointType.IsDefined(typeof(AllowPlatformNoTenantAttribute), inherit: false)
               && httpContext.User.HasClaim(ClaimConstants.Permission, Allow.Platform_Administration);
    }
}
