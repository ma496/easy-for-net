namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /tenants/{id}</c> to return one tenant - its display name, its
/// identifier in both the entered and the normalized form, its lifecycle status and its audit trail -
/// so the administration screens can show a single tenant and fill the form that edits it.
/// </summary>
/// <remarks>
/// Marked <see cref="AllowNoTenantAttribute"/> because a tenant is the scope rather than something
/// inside one: a platform administrator acting in no tenant, and a member who has not yet chosen
/// between the tenants they belong to, both have to be able to read one.
/// Which tenants the caller may read is <see cref="ITenantService.Tenants"/>'s decision alone - every
/// tenant for a caller holding platform administration, and otherwise only the tenants the caller
/// holds an active membership in - so this endpoint cannot disagree with the list about what is
/// visible.
/// <para>
/// Either permission admits the caller. <see cref="Allow.Tenant_View"/> is the grant the tenant list
/// is read with, while <see cref="Allow.Tenant_Detail"/> is the narrower one a tenant administrator
/// is given to open their own tenant's detail screen without being admitted to the platform's list of
/// every tenant. Widening who may ask changes nothing about what comes back: the service above still
/// answers only with tenants the caller has standing in.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TenantGetEndpoint(ITenantService tenantService) : Endpoint<TenantGetRequest, TenantGetResponse>
{
    /// <summary>
    /// The refusal reported for a tenant the caller may not read. Absent, deleted and invisible are
    /// deliberately one message and one code, so the response cannot reveal whether the tenant named
    /// ever existed.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    public override void Configure()
    {
        Get("{id}");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_View, Allow.Tenant_Detail);
    }

    public override async Task HandleAsync(TenantGetRequest request, CancellationToken cancellationToken)
    {
        // Read through the service rather than off the set, so a tenant the caller has no standing in
        // reads as missing here exactly as a deleted or an absent one does.
        var tenant = await tenantService.GetByIdAsync(request.Id, cancellationToken);
        if (tenant == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        await Send.ResponseAsync(new TenantGetResponseMapper().Map(tenant), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant to read by its identity - its primary key, never its url-safe
/// identifier.
/// </summary>
sealed class TenantGetRequest : BaseDto<Guid>
{
}

/// <summary>
/// Validates that the <see cref="TenantGetRequest"/> names a tenant.
/// </summary>
sealed class TenantGetValidator : Validator<TenantGetRequest>
{
    public TenantGetValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload describing one tenant. It derives from <see cref="AuditableDto{TId}"/>, so the
/// creating account and time and the last-updating account and time travel with every tenant read.
/// </summary>
public sealed class TenantGetResponse : AuditableDto<Guid>
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; set; } = null!;
    public TenantStatus Status { get; set; }
}

/// <summary>
/// Maps a <see cref="Tenant"/> onto its <see cref="TenantGetResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantGetResponseMapper
{
    public partial TenantGetResponse Map(Tenant entity);
}
