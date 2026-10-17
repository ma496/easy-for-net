using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Hosting;

namespace Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// This endpoint that handles <c>GET /tenants/host</c> to tell the web app what the host it is served on
/// names, before anybody signs in: the platform, or a tenant with its display name and status.
/// </summary>
/// <remarks>
/// Anonymous, and read from the request's host alone - never from a session, so a signed-in caller is
/// answered exactly as a visitor is. The platform host and a host naming nothing (host resolution off,
/// or a host that is not one of ours) both answer <c>platform</c>. A tenant host answers its tenant while
/// the tenant exists and is not deleted - a suspended one included, with its status, so the web app can
/// say so - and 404 <c>tenantNotFound</c> otherwise. It reveals no more than the sign-in page on that host
/// already would: the name a tenant chose to be addressed by.
/// </remarks>
sealed class TenantHostGetEndpoint(ITenantHostResolver tenantHostResolver, ITenantService tenantService)
    : EndpointWithoutRequest<TenantHostGetResponse>
{
    public override void Configure()
    {
        Get("host");
        Group<TenantsGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var host = tenantHostResolver.Resolve(HttpContext);
        var platformUrl = tenantHostResolver.PlatformUrl();
        var tenantUrlTemplate = tenantHostResolver.TenantUrlTemplate();

        if (host is not { Kind: TenantHostKind.Tenant, Identifier: { } identifier })
        {
            await Send.ResponseAsync(new TenantHostGetResponse
            {
                Kind = TenantHostGetResponse.PlatformKind,
                PlatformUrl = platformUrl,
                TenantUrlTemplate = tenantUrlTemplate
            }, cancellation: cancellationToken);
            return;
        }

        var tenant = await tenantService.FindByIdentifierAsync(identifier, cancellationToken);
        if (tenant is null)
        {
            this.ThrowError(ErrorCodes.TenantNotFound, StatusCodes.Status404NotFound);
        }

        await Send.ResponseAsync(new TenantHostGetResponse
        {
            Kind = TenantHostGetResponse.TenantKind,
            Name = tenant.Name,
            Identifier = tenant.Identifier,
            Status = tenant.Status,
            PlatformUrl = platformUrl,
            TenantUrlTemplate = tenantUrlTemplate
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// What the request's host names: <c>platform</c>, or <c>tenant</c> with the tenant's name, identifier and
/// status. <see cref="PlatformUrl"/> is always present, and <see cref="TenantUrlTemplate"/> whenever host
/// resolution is on; the tenant fields are null for <c>platform</c>.
/// </summary>
sealed class TenantHostGetResponse
{
    /// <summary>The <see cref="Kind"/> of the platform host, and of a host that names nothing.</summary>
    public const string PlatformKind = "platform";

    /// <summary>The <see cref="Kind"/> of a tenant host.</summary>
    public const string TenantKind = "tenant";

    /// <summary>Gets or sets <see cref="PlatformKind"/> or <see cref="TenantKind"/>.</summary>
    public string Kind { get; set; } = null!;

    /// <summary>Gets or sets the tenant's display name, for <see cref="TenantKind"/>.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the tenant's identifier as it was entered, for <see cref="TenantKind"/>.</summary>
    public string? Identifier { get; set; }

    /// <summary>Gets or sets the tenant's status - active or suspended - for <see cref="TenantKind"/>.</summary>
    public TenantStatus? Status { get; set; }

    /// <summary>Gets or sets the platform origin, <c>Web:DefaultDomain</c>.</summary>
    public string PlatformUrl { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tenant origin template, <c>Web:TenantOriginTemplate</c> (for example
    /// <c>http://{tenant}.localhost:3000</c>), with its <c>{tenant}</c> placeholder left in place - so a form
    /// can show the origin a tenant not yet created will be served on - or null when host resolution is off.
    /// </summary>
    public string? TenantUrlTemplate { get; set; }
}
