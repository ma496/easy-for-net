namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>PUT /tenants/{id}</c> to rename a tenant - its display name, its
/// url-safe identifier, or both - refusing the system-created bootstrap tenant and an identifier
/// another tenant already holds.
/// </summary>
/// <remarks>
/// A tenant is the scope rather than a row inside one, so this endpoint needs no active tenant and
/// authorizes the tenant it addresses itself: a tenant the caller may not see is refused exactly as
/// an absent or a deleted one is, so the response never betrays that the tenant exists for somebody
/// else. Nothing is written before all three guards have passed, so a rejected rename persists no
/// part of itself.
/// </remarks>
sealed class TenantUpdateEndpoint(ITenantService tenantService, AppDbContext dbContext)
    : Endpoint<TenantUpdateRequest, TenantUpdateResponse>
{
    public override void Configure()
    {
        Put("{id}");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_Update);
    }

    public override async Task HandleAsync(TenantUpdateRequest request, CancellationToken cancellationToken)
    {
        // Visibility first. A tenant that never existed, one that has been deleted and one that belongs
        // to somebody else are all the same answer here, which is what keeps this surface from
        // disclosing which of the three it was.
        var entity = await tenantService.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            ThrowError("Tenant not found", ErrorCodes.TenantNotFound);
        }

        // The bootstrap tenant that every pre-existing row was attributed to cannot be renamed: the
        // seeded data and the upgrade path are pinned to the identifier it was created with.
        if (entity.SystemCreated)
        {
            ThrowError("System-created tenant cannot be modified", ErrorCodes.SystemCreatedTenantCannotBeModified);
        }

        // Compared on the stored normalized lower-case form and across retained rows, so an identifier
        // freed only by deleting a tenant stays taken, while this tenant keeping its own identifier is
        // not reported as a duplicate of itself. The unique index on that column is the backstop when
        // two renames ask at once. Raised against the identifier field, so the caller is told which
        // value was refused.
        if (await tenantService.IdentifierExistsAsync(request.Identifier, request.Id, cancellationToken))
        {
            ThrowError(x => x.Identifier, ITenantService.DuplicateIdentifierMessage, ErrorCodes.TenantIdentifierAlreadyExists);
        }

        var requestMapper = new TenantUpdateRequestMapper();
        requestMapper.Update(request, entity);

        // Stored trimmed as entered, exactly as creation stores it, so the value read back is the one
        // the length and shape rules were applied to. The normalized identifier beside it is recomputed
        // by the entity on save, and the updating account and the update time are stamped centrally
        // there too.
        entity.Name = entity.Name.Trim();
        entity.Identifier = entity.Identifier.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        var responseMapper = new TenantUpdateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for renaming a tenant, carrying the tenant's id from the route and the display
/// name and identifier it should carry from here on.
/// </summary>
public sealed class TenantUpdateRequest : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for a tenant update request. The display-name and identifier rules are the
/// shared ones, so a rename is held to exactly the bounds and the shape creation and self-service
/// sign-up are held to, and every failure names the field that broke them.
/// </summary>
sealed class TenantUpdateValidator : Validator<TenantUpdateRequest>
{
    public TenantUpdateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().TenantName();
        RuleFor(x => x.Identifier).NotEmpty().TenantIdentifier();
    }
}

/// <summary>
/// Response payload returned after a successful rename, echoing the tenant as it now stands -
/// including the normalized identifier the uniqueness rule is enforced on.
/// </summary>
public sealed class TenantUpdateResponse : BaseDto<Guid>
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; set; } = null!;
    public TenantStatus Status { get; set; }
}

/// <summary>
/// This mapper that updates a <see cref="Tenant"/> entity in-place from a
/// <see cref="TenantUpdateRequest"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class TenantUpdateRequestMapper
{
    public partial void Update(TenantUpdateRequest request, Tenant entity);
}

/// <summary>
/// This mapper that projects a <see cref="Tenant"/> entity into a <see cref="TenantUpdateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantUpdateResponseMapper
{
    public partial TenantUpdateResponse Map(Tenant entity);
}