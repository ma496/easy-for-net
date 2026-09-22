namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants</c> to create a tenant from the platform, persisting
/// it in the active state and returning its assigned identity.
/// </summary>
/// <remarks>
/// Usable with no tenant established, because a tenant is the scope rather than something
/// inside one: a platform administrator creates tenants without acting in any of them. The exemption
/// covers the tenant requirement alone - the caller is still authenticated and still has to hold
/// <see cref="Allow.Tenant_Create"/>, which is a platform-tier permission no tenant role can carry.
/// The creation itself is delegated to <see cref="ITenantService.CreateAsync"/>, the single creation
/// path this endpoint shares with self-service sign-up, so the trimming, the state the row is
/// persisted in and the system-created administrator role the tenant is provisioned with cannot
/// differ between the two surfaces. No membership is created here: a tenant created from the
/// platform has no first member until one is added.
/// </remarks>
sealed class TenantCreateEndpoint(ITenantService tenantService) : Endpoint<TenantCreateRequest, TenantCreateResponse>
{
    public override void Configure()
    {
        Post("");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_Create);
    }

    public override async Task HandleAsync(TenantCreateRequest request, CancellationToken cancellationToken)
    {
        // The comparison runs against the stored normalized form and deliberately counts soft-deleted
        // tenants, so an identifier differing only in case or freed only by deletion is still taken.
        // The refusal is attributed to the identifier field and nothing is persisted; the unique index
        // on that same column is the backstop when two requests ask at once.
        var identifierExists = await tenantService.IdentifierExistsAsync(request.Identifier, cancellationToken: cancellationToken);
        if (identifierExists)
        {
            ThrowError(x => x.Identifier, ITenantService.DuplicateIdentifierMessage, ErrorCodes.TenantIdentifierAlreadyExists);
        }

        var requestMapper = new TenantCreateRequestMapper();
        var entity = requestMapper.Map(request);
        // The status, the system-created flag, the trimming, the normalized identifier and the audit
        // values are all the creation path's and the context's to set - none of them is taken from the
        // payload.
        entity = await tenantService.CreateAsync(entity, cancellationToken: cancellationToken);

        var responseMapper = new TenantCreateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for creating a tenant, carrying its display name and its url-safe identifier and
/// nothing else: lifecycle status, audit values and the system-created flag are never a caller's to
/// supply.
/// </summary>
public sealed class TenantCreateRequest
{
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules ensuring a create-tenant request supplies a display name and an identifier
/// that satisfy the shared tenant naming rules, so that every failure names the offending field.
/// </summary>
sealed class TenantCreateValidator : Validator<TenantCreateRequest>
{
    public TenantCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().TenantName();
        RuleFor(x => x.Identifier).NotEmpty().TenantIdentifier();
    }
}

/// <summary>
/// Response payload returned after a successful tenant creation, echoing the assigned identity, the
/// name and identifier as they were stored, the normalized identifier maintained beside them and the
/// state the tenant was persisted in.
/// </summary>
public sealed class TenantCreateResponse : BaseDto<Guid>
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; set; } = null!;
    public TenantStatus Status { get; set; }
}

/// <summary>
/// This mapper that projects a <see cref="TenantCreateRequest"/> into a <see cref="Tenant"/> entity.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class TenantCreateRequestMapper
{
    public partial Tenant Map(TenantCreateRequest request);
}

/// <summary>
/// This mapper that projects a created <see cref="Tenant"/> entity into a
/// <see cref="TenantCreateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantCreateResponseMapper
{
    public partial TenantCreateResponse Map(Tenant entity);
}
