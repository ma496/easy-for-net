namespace Backend.Processors;

using System.Security.Claims;
using Backend.Features.Identity.Core;
using Backend.Middleware;
using FluentValidation.Results;

/// <summary>
/// Global FastEndpoints pre-processor that establishes the tenant a request acts in, and refuses the
/// request when it may not act in one. It is the single place the rule lives: every endpoint that
/// does not carry <see cref="AllowNoTenantAttribute"/> requires an established tenant that exists,
/// is not suspended, and that the caller still holds an active membership in, so no endpoint repeats
/// the check and none can forget it.
/// </summary>
/// <remarks>
/// <para>
/// It costs no query. The session check that runs earlier in the pipeline has already read the
/// tenant and the membership to recompute what the request is authorized to do, and left its verdict
/// on the request; this processor only reads that verdict, reports it, and turns a good one into an
/// established tenant scope. A refusal never ends the session: the caller stays authenticated, is
/// told which of the four things went wrong, and can choose another tenant they belong to. Only the
/// password check in the session middleware still signs anyone out.
/// </para>
/// <para>
/// This is the single place the rule is <i>decided</i>, but not the only place it can be what refuses
/// a request. Endpoint authorization runs before it and reads the permissions the request holds, which
/// a stale tenant selection leaves empty, so that refusal is produced there rather than here;
/// <see cref="TenantRefusalResultHandler"/> answers it with the same description this uses. The two
/// agree because the description is stated once, in <see cref="TenantRefusal"/>.
/// </para>
/// </remarks>
public sealed class TenantContextProcessor : IGlobalPreProcessor
{
    /// <summary>
    /// Establishes the request's tenant scope, or short-circuits the request with a 403 naming the
    /// reason the operation is refused.
    /// </summary>
    /// <param name="context">The pre-processor context for the request being handled.</param>
    /// <param name="ct">Token used to cancel writing the refusal.</param>
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;

        // An unauthenticated request is not this processor's business: it is either refused by
        // authentication with a 401 or allowed through because the endpoint permits anonymous
        // callers, and in neither case is there a session that could name a tenant. Its scope stays
        // unresolved, so anything tenant-scoped it were to read or write still fails loudly.
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // The tenant context is resolved from the request's own service scope rather than injected:
        // a global pre-processor is instantiated once for the application, and holding a per-request
        // service in it would hand every request whichever tenant the first one established.
        var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        var sessionTenantId = ReadSessionTenantId(httpContext.User);

        // A session that names no tenant carries no verdict worth reading: it is the caller who holds
        // no active membership at all, or who holds several and has not yet chosen between them.
        var status = sessionTenantId is null
            ? TenantSessionStatus.NoActiveTenant
            : TenantSessionState.Read(httpContext);

        if (status == TenantSessionStatus.Active && sessionTenantId is { } tenantId)
        {
            // The scope is opened for the rest of the request and the handle is deliberately dropped:
            // disposing it here would restore the unresolved state before the endpoint runs, and the
            // context belongs to the request's own scope, so it ends with the request either way.
            // Endpoints exempt from the requirement get the scope too - an upload attributes its file
            // to this tenant, and a member listing reads inside it - because they are exempt from the
            // refusal, not from acting in the tenant their session names.
            _ = tenantContext.BeginTenant(tenantId);
            return;
        }

        if (IsExemptFromTenantRequirement(httpContext))
        {
            // Account self-service, tenant selection and onboarding have to keep working for a caller
            // with no usable tenant - that is how such a caller gets one - so the scope is resolved
            // to the platform rather than left unresolved: they read and write the rows that belong
            // to no tenant instead of failing on a scope that was never established.
            _ = tenantContext.BeginPlatformScope();
            return;
        }

        var (message, errorCode) = TenantRefusal.Describe(status);
        await httpContext.Response.SendErrorsAsync(
            [new ValidationFailure(string.Empty, message) { ErrorCode = errorCode }],
            StatusCodes.Status403Forbidden,
            cancellation: ct);
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

    /// <summary>
    /// Reports whether the endpoint being called is exempt from the active tenant requirement, by
    /// reading <see cref="AllowNoTenantAttribute"/> off the endpoint's own type. A request whose
    /// endpoint cannot be identified is treated as not exempt, so an operation whose tenant rule
    /// cannot be established is refused rather than allowed to run unrestricted.
    /// </summary>
    /// <param name="httpContext">The request being handled.</param>
    /// <returns><see langword="true"/> when the endpoint runs with no tenant established.</returns>
    private static bool IsExemptFromTenantRequirement(HttpContext httpContext)
    {
        var definition = httpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();

        return definition is not null
               && definition.EndpointType.IsDefined(typeof(AllowNoTenantAttribute), inherit: false);
    }
}