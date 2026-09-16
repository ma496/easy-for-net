namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /tenants/{id}</c> to retire a tenant, refusing the
/// system-created bootstrap tenant. The tenant is soft-deleted: its row and everything attributed to
/// it stay in storage and simply stop being reachable.
/// </summary>
/// <remarks>
/// A tenant is the scope rather than a row inside one, so this endpoint needs no active tenant and
/// authorizes the tenant it addresses itself: a tenant that never existed, one already deleted and
/// one the caller may not see are all refused with the same message and the same
/// <see cref="ErrorCodes.TenantNotFound"/>, so the response never betrays that the tenant exists for
/// somebody else. Deletion is not enforced against live sessions here. Members of a deleted tenant
/// keep their sessions and stay signed in; their next tenant-scoped request is refused by
/// <c>TenantContextProcessor</c> with the same not-found code, which leaves their access to every
/// other tenant they belong to intact and lets the web app offer them one of those instead.
/// </remarks>
[AllowNoTenant]
sealed class TenantDeleteEndpoint(ITenantService tenantService, AppDbContext dbContext)
    : Endpoint<TenantDeleteRequest, TenantDeleteResponse>
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
    private const string SystemCreatedMessage = "System-created tenant cannot be deleted";

    public override void Configure()
    {
        Delete("{id}");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_Delete);
    }

    public override async Task HandleAsync(TenantDeleteRequest request, CancellationToken cancellationToken)
    {
        // Visibility first, read through the service so this surface agrees with the list and the
        // read about which tenants exist for this caller at all.
        var entity = await tenantService.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        // The bootstrap tenant that every pre-existing row was attributed to cannot be deleted: the
        // seeded data and the upgrade path are pinned to it.
        if (entity.SystemCreated)
        {
            ThrowError(SystemCreatedMessage, ErrorCodes.SystemCreatedTenantCannotBeModified);
        }

        // Removing an ISoftDelete entity is turned into a soft delete by AppDbContext, so the row is
        // retained with its identifier still reserved while the soft-delete query filter takes it out
        // of every query - and the tenant filter then takes everything attributed to it out too.
        dbContext.Tenants.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.ResponseAsync(new() { Success = true }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the tenant to delete by id.
/// </summary>
sealed class TenantDeleteRequest : BaseDto<Guid>
{
}

/// <summary>
/// FluentValidation rules requiring a non-empty id for tenant deletion.
/// </summary>
sealed class TenantDeleteValidator : Validator<TenantDeleteRequest>
{
    public TenantDeleteValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload indicating the outcome of a tenant deletion attempt.
/// </summary>
public sealed class TenantDeleteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}