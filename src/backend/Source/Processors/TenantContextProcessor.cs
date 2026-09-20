namespace Backend.Processors;

using System.Security.Claims;
using Backend.Features.Identity.Core;

/// <summary>
/// Global FastEndpoints pre-processor that establishes the tenant a request acts in, from the tenant
/// its session names and from nothing else. It is the single place the scope is opened, so no
/// endpoint repeats it and none can forget it.
/// </summary>
/// <remarks>
/// <para>
/// It costs no query. The tenant travels in the session's own claims, minted when the session was
/// established and re-minted whenever it is renewed or switched, so establishing the scope is a claim
/// lookup rather than a database read.
/// </para>
/// <para>
/// A session naming no tenant acts in the platform scope: it reads and writes the rows that belong to
/// no tenant. That is not a privilege - what such a session may do is decided by its permission
/// claims, which are narrowed to the platform scope when it is minted, and an ordinary account
/// exercises nothing at all outside a tenant. A tenant-scoped operation is therefore kept out of
/// platform scope by declaring a <see cref="PermissionScope.Tenant"/> permission, which no
/// platform-scope session carries, rather than by any rule stated here.
/// </para>
/// </remarks>
public sealed class TenantContextProcessor : IGlobalPreProcessor
{
    /// <summary>
    /// Establishes the request's tenant scope: the tenant its session names, or the platform scope
    /// when it names none.
    /// </summary>
    /// <param name="context">The pre-processor context for the request being handled.</param>
    /// <param name="ct">Token used to cancel the work; nothing here is cancellable.</param>
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;

        // An unauthenticated request is not this processor's business: it is either refused by
        // authentication with a 401 or allowed through because the endpoint permits anonymous
        // callers, and in neither case is there a session that could name a tenant. Its scope stays
        // unresolved, so anything tenant-scoped it were to read or write still fails loudly, and the
        // few anonymous endpoints that do touch data establish the scope they need themselves.
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // The tenant context is resolved from the request's own service scope rather than injected:
        // a global pre-processor is instantiated once for the application, and holding a per-request
        // service in it would hand every request whichever tenant the first one established.
        var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();

        // The scope is opened for the rest of the request and the handle is deliberately dropped:
        // disposing it here would restore the unresolved state before the endpoint runs, and the
        // context belongs to the request's own scope, so it ends with the request either way.
        _ = ReadSessionTenantId(httpContext.User) is { } tenantId
            ? tenantContext.BeginTenant(tenantId)
            : tenantContext.BeginPlatformScope();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the identifier of the tenant the session names, or <see langword="null"/> when it names
    /// none or names one that is not a well-formed identifier.
    /// </summary>
    /// <param name="principal">The principal the request authenticated as.</param>
    /// <returns>The tenant the session was established for, if it names one.</returns>
    private static Guid? ReadSessionTenantId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimConstants.TenantId), out var tenantId)
            ? tenantId
            : null;
}
