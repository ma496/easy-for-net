using Backend.Features.Identity.Core;
using Backend.Features.Localization.Core;
using Backend.Features.Tenancy.Core.Hosting;

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
    public async Task InvokeAsync(HttpContext context, ITenantHostResolver tenantHostResolver, IErrorMessageLocalizer localizer)
    {
        if (context.User.Identity?.IsAuthenticated == true && !BelongsOn(context, tenantHostResolver.Resolve(context)))
        {
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
