namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/{id}/suspend</c> to put a tenant out of service
/// without touching anything inside it: the tenant's rows, roles and memberships are all retained
/// exactly as they were, and only its lifecycle status changes.
/// </summary>
/// <remarks>
/// Once the status is saved, every session acting in the tenant is ended: access tokens answer 401 on
/// the next request and refresh tokens are refused, so members sign in again - to another tenant they
/// belong to, since sign-in into a suspended one is refused. Sessions in other tenants and in platform
/// scope are untouched. Suspending a tenant that is already suspended is accepted and changes nothing
/// but revokes again, harmlessly, because suspension describes a state to reach rather than a
/// transition to make.
/// </remarks>
sealed class TenantSuspendEndpoint(ITenantService tenantService, AppDbContext dbContext, ISessionRevocationService sessionRevocationService) : Endpoint<TenantSuspendRequest, TenantSuspendResponse>
{
    public override void Configure()
    {
        Post("{id}/suspend");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_Suspend);
    }

    public override async Task HandleAsync(TenantSuspendRequest request, CancellationToken cancellationToken)
    {
        // Read through the service rather than off the set, so a tenant the caller has no standing in
        // reads as missing here exactly as a deleted or an absent one does.
        var tenant = await tenantService.GetByIdAsync(request.Id, cancellationToken);
        if (tenant == null)
        {
            this.ThrowError(ErrorCodes.TenantNotFound);
        }

        if (tenant.SystemCreated)
        {
            this.ThrowError(ErrorCodes.SystemCreatedTenantCannotBeModified);
        }

        // The status is the only thing written: nothing the tenant owns is deleted, detached or
        // rewritten, so reactivating later restores the tenant exactly as it was left.
        tenant.Status = TenantStatus.Suspended;
        await dbContext.SaveChangesAsync(cancellationToken);
        await sessionRevocationService.RevokeTenantAsync(tenant.Id, cancellationToken);

        await Send.ResponseAsync(new TenantSuspendResponseMapper().Map(tenant), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant to suspend by its identity.
/// </summary>
sealed class TenantSuspendRequest : BaseDto<Guid>
{
}

/// <summary>
/// Validates that the <see cref="TenantSuspendRequest"/> names a tenant.
/// </summary>
sealed class TenantSuspendValidator : Validator<TenantSuspendRequest>
{
    public TenantSuspendValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload reporting the tenant's lifecycle status after the suspension, so the caller can
/// confirm the new state without reading the tenant again.
/// </summary>
public sealed class TenantSuspendResponse : BaseDto<Guid>
{
    public TenantStatus Status { get; set; }
}

/// <summary>
/// Maps a suspended <see cref="Tenant"/> onto its <see cref="TenantSuspendResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantSuspendResponseMapper
{
    public partial TenantSuspendResponse Map(Tenant entity);
}
