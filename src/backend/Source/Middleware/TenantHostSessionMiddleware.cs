using Backend.Features.Identity.Core;
using Backend.Features.Localization.Core;
using RefreshTokenIssuer = Backend.Features.Identity.Endpoints.Account.TokenService;
using Backend.Features.Tenancy.Core.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Backend.Middleware;

/// <summary>
/// Binds every authenticated request to the host its session belongs on: a session acting in tenant
/// <c>acme</c> is honoured on <c>acme</c>'s host alone, and a session acting in no tenant on the platform
/// host alone. Anything else answers 403 <c>tenantHostMismatch</c>.
/// </summary>
/// <remarks>
/// <para>
/// The host chooses, it never grants: the session stays the only authority, so this middleware can only
/// refuse. It compares the host against the claims <c>SessionClaims.Project</c> put on the principal -
/// <c>tenant_identifier</c> and <c>tenant_id</c> - and reads nothing else, so it adds no read to an
/// ordinary request.
/// </para>
/// <para>
/// Registered straight after <see cref="SessionStoreUnavailableMiddleware"/>, so a store outage has
/// already been answered with 503 before this runs, and ahead of authorization and every endpoint -
/// the notification hub's upgrade included. An unauthenticated request passes untouched (the endpoint
/// decides what an anonymous caller gets), and so does every request when the host names nothing
/// (<see cref="TenantHostKind.None"/>: resolution off, or a host that is not one of ours).
/// </para>
/// <para>
/// A mismatched session must never lock a browser out of the host. An auth cookie is sent on every
/// request to its host, so a cookie whose session belongs elsewhere - one issued before sessions recorded
/// their tenant's identifier, say - would otherwise have sign-in, ticket redemption and refresh refused
/// too, and nothing would ever replace it. So an endpoint that admits anonymous callers is reached as an
/// anonymous caller - the session grants nothing there, and a sign-in replaces the cookie - and refusing
/// any other endpoint also deletes the auth and refresh-token cookies, so the browser's next request is
/// anonymous and the web app sends it to sign in.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class TenantHostSessionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Refuses an authenticated request whose session does not belong on the host it arrived on.
    /// </summary>
    /// <param name="context">The request being handled.</param>
    /// <param name="tenantHostResolver">Reads what the host names.</param>
    /// <param name="localizer">Localizes the refusal into the request's culture.</param>
    /// <param name="cookieOptions">Names the auth cookie, so only a request carrying one has it deleted.</param>
    public async Task InvokeAsync(
        HttpContext context,
        ITenantHostResolver tenantHostResolver,
        IErrorMessageLocalizer localizer,
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        if (context.User.Identity?.IsAuthenticated == true && !BelongsOn(context, tenantHostResolver.Resolve(context)))
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
                await next(context);
                return;
            }

            var authCookieName = cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name;
            if (authCookieName is not null && context.Request.Cookies.ContainsKey(authCookieName))
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Cookies.Delete(RefreshTokenIssuer.RefreshTokenCookieName);
            }

            await CodedErrorResponse.WriteAsync(context, localizer, StatusCodes.Status403Forbidden, ErrorCodes.TenantHostMismatch, context.RequestAborted);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Whether the session projected onto the request may act on this host.
    /// </summary>
    private static bool BelongsOn(HttpContext context, TenantHost host)
        => host.Kind switch
        {
            TenantHostKind.Tenant => string.Equals(
                context.User.FindFirst(ClaimConstants.TenantIdentifier)?.Value,
                host.Identifier,
                StringComparison.OrdinalIgnoreCase),
            TenantHostKind.Platform => context.User.FindFirst(ClaimConstants.TenantId) is null,
            _ => true,
        };
}
