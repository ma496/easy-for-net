using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Hosting;

namespace Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// This endpoint that handles <c>GET /tenants/tls-allowed?domain=</c>, the question a reverse proxy issuing
/// certificates on demand asks before it requests one: is this domain one of ours?
/// </summary>
/// <remarks>
/// Anonymous, since the proxy carries no credential. It answers 200 only when <c>domain</c> - parsed by the
/// same rules a request's host is - names the platform host or a tenant that exists and is not deleted
/// (suspended included, so its host still serves its sign-in page), and 404 otherwise: an unknown label, an
/// unrelated domain, a malformed value, and every domain while host resolution is off. Refusing all but
/// those is what keeps anybody pointing a domain at the server from making it request certificates for
/// them. It is held to the same request rate limit as every other endpoint.
/// </remarks>
sealed class TenantTlsAllowedEndpoint(ITenantHostResolver tenantHostResolver, ITenantService tenantService)
    : Endpoint<TenantTlsAllowedRequest, EmptyResponse>
{
    public override void Configure()
    {
        Get("tls-allowed");
        Group<TenantsGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(TenantTlsAllowedRequest request, CancellationToken cancellationToken)
    {
        var host = tenantHostResolver.ResolveHost(request.Domain?.Trim());

        var allowed = host.Kind switch
        {
            TenantHostKind.Platform => true,
            TenantHostKind.Tenant => await tenantService.FindByIdentifierAsync(host.Identifier!, cancellationToken) is not null,
            _ => false
        };

        if (!allowed)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.OkAsync(cancellationToken);
    }
}

/// <summary>Request naming the domain a certificate would be issued for.</summary>
sealed class TenantTlsAllowedRequest
{
    /// <summary>Gets or sets the domain, a host name with no scheme or path.</summary>
    [QueryParam]
    public string? Domain { get; set; }
}
