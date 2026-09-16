namespace Backend.Middleware;

using Backend.Features.Identity.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

/// <summary>
/// Rebuilds what every authenticated request is authorized to do from current data, before the
/// request is authorized. It sits between authentication and authorization because that is the only
/// point at which the claims an endpoint's permission policy reads can still be replaced, so the
/// policy is evaluated against the roles and permissions the account holds now rather than the ones
/// its session was minted with.
/// </summary>
/// <remarks>
/// The two failures it can find are answered differently and deliberately so. A session that no
/// longer belongs to a usable account - most often because the password changed - is over: the
/// principal is dropped and the caller is signed out. A tenant that has been suspended, deleted or
/// left is not: the caller stays authenticated, loses only what that tenant granted them, and the
/// reason is recorded on the request so the tenant enforcement point can refuse the operation with
/// an explanation and offer them another tenant.
/// </remarks>
public sealed class SessionValidationMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Re-evaluates the request's session, then either ends it or replaces its role and permission
    /// claims with the ones current data grants, and passes the request on either way.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <param name="dbContext">The database context the session is re-read through.</param>
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var session = await SessionValidator.EvaluateAsync(context.User, dbContext, context.RequestAborted);
        if (!session.IsCurrent)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            context.Response.Cookies.Delete("refreshToken");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        else
        {
            TenantSessionState.Record(context, session.TenantStatus);
            context.User = WithCurrentAuthorization(context.User, session);
        }

        await next(context);
    }

    /// <summary>
    /// Returns the principal with every role and permission claim it arrived with replaced by the
    /// ones the session is entitled to now. Identity claims are carried over untouched - the account,
    /// its name, its session version and the tenant its session names - so the caller stays exactly
    /// who they were and only what they may do is recomputed.
    /// </summary>
    /// <param name="principal">The principal the request authenticated as.</param>
    /// <param name="session">The session state read from current data.</param>
    /// <returns>A principal carrying the current authorization.</returns>
    private static ClaimsPrincipal WithCurrentAuthorization(ClaimsPrincipal principal, SessionState session)
    {
        var identity = principal.Identity as ClaimsIdentity;
        var incomingRoleClaimType = identity?.RoleClaimType ?? ClaimTypes.Role;

        var claims = principal.Claims
            .Where(claim => claim.Type != incomingRoleClaimType
                            && claim.Type != ClaimTypes.Role
                            && claim.Type != ClaimConstants.Permission)
            .ToList();
        claims.AddRange(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(session.Permissions.Select(permission => new Claim(ClaimConstants.Permission, permission)));

        // The authentication type is carried over because it is what keeps the rebuilt identity
        // authenticated, and it is read off the identity itself rather than off its claims-based
        // view, so a principal that authenticated as something other than a claims identity is not
        // silently reduced to an anonymous one. The role claim type is the application's own, which
        // is what the endpoint authorization is configured to read.
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            principal.Identity?.AuthenticationType,
            identity?.NameClaimType ?? ClaimTypes.Name,
            ClaimTypes.Role));
    }
}

/// <summary>
/// The request's own record of whether the tenant its session names may be acted in. The session
/// check writes it once, having already read the tenant and the membership, and the tenant
/// enforcement point reads it to decide whether to refuse the operation and which reason to report,
/// so neither the tenant nor the membership is read twice per request.
/// </summary>
static class TenantSessionState
{
    private const string ItemKey = "Backend.Tenancy.SessionStatus";

    /// <summary>
    /// Records the state of the request's tenant selection. Only the session check calls this.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <param name="status">The state read from current data.</param>
    public static void Record(HttpContext context, TenantSessionStatus status)
        => context.Items[ItemKey] = status;

    /// <summary>
    /// Reads the state recorded for the request. A request that carried no usable session - an
    /// anonymous one, or one that was signed out - has none recorded and reads as naming no tenant,
    /// which is the state that lets nothing tenant-scoped through.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <returns>The recorded state, or <see cref="TenantSessionStatus.NoActiveTenant"/> when none was recorded.</returns>
    public static TenantSessionStatus Read(HttpContext context)
        => context.Items.TryGetValue(ItemKey, out var recorded) && recorded is TenantSessionStatus status
            ? status
            : TenantSessionStatus.NoActiveTenant;
}
