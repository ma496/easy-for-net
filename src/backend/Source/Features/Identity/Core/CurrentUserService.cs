namespace Backend.Features.Identity.Core;

using System.Security.Claims;
using Backend.Attributes;

/// <summary>
/// Provides access to the identity, claims, roles, and permissions of the user making the current HTTP request.
/// </summary>
[AllowOutside]
public interface ICurrentUserService
{
    Guid? GetCurrentUserId();
    string? GetCurrentUsername();
    string? GetCurrentEmail();
    bool IsAuthenticated();
    bool IsInRole(string role);
    bool HasPermission(string permission);

    /// <summary>
    /// Whether the caller's account belongs to the platform tier. This answers which tier the caller
    /// works in, never what they may do - that stays a question for <see cref="HasPermission"/>, whose
    /// answers are already narrowed to the scope the request is acting in. Use it only where the tier
    /// itself is the question: exempting an endpoint from the active-tenant requirement, entering a
    /// tenant without a membership, leaving one again, and deciding what a newly created account
    /// becomes.
    /// </summary>
    /// <returns><see langword="true"/> when the caller is a platform account.</returns>
    bool IsPlatform();
    IEnumerable<string> GetCurrentUserRoles();
    IEnumerable<string> GetCurrentUserPermissions();
}

/// <summary>
/// Default <see cref="ICurrentUserService"/> implementation that reads values from the <see cref="ClaimsPrincipal"/>
/// exposed by the current <see cref="HttpContext"/>.
/// </summary>
[NoDirectUse]
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly ClaimsPrincipal? _user = httpContextAccessor.HttpContext?.User;

    public Guid? GetCurrentUserId()
    {
        var userIdClaim = _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }

    public string? GetCurrentUsername()
    {
        return _user?.FindFirst(ClaimTypes.Name)?.Value;
    }

    public string? GetCurrentEmail()
    {
        return _user?.FindFirst(ClaimTypes.Email)?.Value;
    }

    public bool IsAuthenticated()
    {
        return _user?.Identity?.IsAuthenticated ?? false;
    }

    public bool IsInRole(string role)
    {
        return _user?.IsInRole(role) ?? false;
    }

    public bool HasPermission(string permission)
    {
        var permissions = GetCurrentUserPermissions();
        return permissions.Contains(permission);
    }

    public bool IsPlatform()
    {
        return _user?.HasClaim(ClaimConstants.IsPlatform, bool.TrueString) ?? false;
    }

    public IEnumerable<string> GetCurrentUserRoles()
    {
        var roles = _user?.FindAll(ClaimTypes.Role).Select(x => x.Value);
        return roles ?? [];
    }

    public IEnumerable<string> GetCurrentUserPermissions()
    {
        var permissions = _user?.FindAll(ClaimConstants.Permission).Select(x => x.Value);
        return permissions ?? [];
    }
}