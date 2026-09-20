namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/{id}/reactivate</c> to return a suspended tenant to
/// service, restoring normal access for every one of its members without any action on their part.
/// </summary>
/// <remarks>
/// Suspension never removed anything, so reactivation has nothing to restore beyond the lifecycle
/// status itself: the tenant's rows, roles and memberships stayed exactly as they were, and the next
/// request each member makes is admitted by <c>TenantContextProcessor</c> again with no sign-out, no
/// sign-in and nothing for them to do. Reactivating a tenant that is already active is accepted and
/// changes nothing, because reactivation describes a state to reach rather than a transition to make
/// - which is also why the system-created bootstrap tenant, a tenant that can never be suspended, is
/// simply a no-op here rather than a refusal.
/// </remarks>
sealed class TenantReactivateEndpoint(ITenantService tenantService, AppDbContext dbContext) : Endpoint<TenantReactivateRequest, TenantReactivateResponse>
{
    /// <summary>
    /// The refusal reported for a tenant the caller may not act on. Absent, deleted and invisible are
    /// deliberately one message and one code, so the response cannot reveal whether the tenant named
    /// ever existed.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    public override void Configure()
    {
        Post("{id}/reactivate");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_Reactivate);
    }

    public override async Task HandleAsync(TenantReactivateRequest request, CancellationToken cancellationToken)
    {
        // Read through the service rather than off the set, so a tenant the caller has no standing in
        // reads as missing here exactly as a deleted or an absent one does.
        var tenant = await tenantService.GetByIdAsync(request.Id, cancellationToken);
        if (tenant == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        // The status is the only thing written, and it is the only thing suspension ever changed, so
        // the tenant comes back exactly as it was left.
        tenant.Status = TenantStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.ResponseAsync(new TenantReactivateResponseMapper().Map(tenant), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant to reactivate by its identity.
/// </summary>
sealed class TenantReactivateRequest : BaseDto<Guid>
{
}

/// <summary>
/// Validates that the <see cref="TenantReactivateRequest"/> names a tenant.
/// </summary>
sealed class TenantReactivateValidator : Validator<TenantReactivateRequest>
{
    public TenantReactivateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload reporting the tenant's lifecycle status after the reactivation, so the caller can
/// confirm the new state without reading the tenant again.
/// </summary>
public sealed class TenantReactivateResponse : BaseDto<Guid>
{
    public TenantStatus Status { get; set; }
}

/// <summary>
/// Maps a reactivated <see cref="Tenant"/> onto its <see cref="TenantReactivateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantReactivateResponseMapper
{
    public partial TenantReactivateResponse Map(Tenant entity);
}