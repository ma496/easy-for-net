namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/{id}/suspend</c> to put a tenant out of service
/// without touching anything inside it: the tenant's rows, roles and memberships are all retained
/// exactly as they were, and only its lifecycle status changes.
/// </summary>
/// <remarks>
/// Suspension is not enforced here. Members of a suspended tenant keep their sessions and stay
/// signed in; their next tenant-scoped request is refused by <c>TenantContextProcessor</c> with
/// <see cref="ErrorCodes.TenantSuspended"/>, which leaves their access to every other tenant they
/// belong to intact and lets the web app offer them one of those instead. Suspending a tenant that
/// is already suspended is accepted and changes nothing, because suspension describes a state to
/// reach rather than a transition to make.
/// </remarks>
sealed class TenantSuspendEndpoint(ITenantService tenantService, AppDbContext dbContext) : Endpoint<TenantSuspendRequest, TenantSuspendResponse>
{
    /// <summary>
    /// The refusal reported for a tenant the caller may not act on. Absent, deleted and invisible are
    /// deliberately one message and one code, so the response cannot reveal whether the tenant named
    /// ever existed.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    /// <summary>
    /// The refusal reported when the addressed tenant is the system-created bootstrap tenant, which
    /// the application depends on and so may not be renamed, suspended or deleted.
    /// </summary>
    private const string SystemCreatedMessage = "The system-created tenant cannot be suspended";

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
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        if (tenant.SystemCreated)
        {
            ThrowError(SystemCreatedMessage, ErrorCodes.SystemCreatedTenantCannotBeModified);
        }

        // The status is the only thing written: nothing the tenant owns is deleted, detached or
        // rewritten, so reactivating later restores the tenant exactly as it was left.
        tenant.Status = TenantStatus.Suspended;
        await dbContext.SaveChangesAsync(cancellationToken);

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