namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/onboard</c> to let an account that already exists
/// create a tenant for itself, becoming that tenant's first member and its administrator, and
/// carrying on in the new tenant without signing in again.
/// </summary>
/// <remarks>
/// Authentication alone is the gate: the endpoint declares no permission, because creating one's own
/// tenant is not an authority granted from the platform, and it is deliberately not anonymous,
/// because onboarding attaches a tenant to an existing account rather than bringing one into being -
/// an unauthenticated caller is refused with 401 and nothing is written.
/// <para>
/// Marked <see cref="AllowNoTenantAttribute"/> because this is one of the endpoints that establishes
/// a tenant: an account holding no usable membership is precisely the caller it exists for, so
/// requiring an active tenant here would shut the only door out of that state.
/// </para>
/// <para>
/// The tenant is created through <see cref="ITenantService.CreateAsync"/>, the same single creation
/// path the platform create endpoint uses, so the trimming, the state the row is persisted in, the
/// duplicate comparison guarding it and the system-created administrator role the tenant is
/// provisioned with are the same code on both surfaces and cannot drift apart. What onboarding adds
/// is the first member: the caller is given an active membership of the new tenant and that
/// administrator role, which confers authority inside the tenant just created and nowhere else -
/// the role carries tenant-tier permissions only, so no platform-tier permission is granted here or
/// anywhere along this path. How many tenants the caller already belongs to is never asked.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TenantOnboardEndpoint(ITenantService tenantService,
                                   ITenantAuthorizationService tenantAuthorizationService,
                                   ICurrentUserService currentUserService) : Endpoint<TenantOnboardRequest, TenantOnboardResponse>
{
    public override void Configure()
    {
        Post("onboard");
        Group<TenantsGroup>();
    }

    public override async Task HandleAsync(TenantOnboardRequest request, CancellationToken cancellationToken)
    {
        // The endpoint is not anonymous, so an unauthenticated caller is already refused before this
        // line is reached; asking again is what makes the creator's identity a value rather than an
        // assumption, since the tenant is created in that account's name and given to it.
        var callerId = currentUserService.GetCurrentUserId();
        if (callerId is not { } creatorUserId)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        // The same comparison the platform create surface makes, through the same service: it reads
        // the stored normalized form and deliberately counts soft-deleted tenants, so an identifier
        // differing only in case or freed only by deletion is still taken. The refusal is attributed
        // to the identifier field with the same code, so a duplicate reads the same however it was
        // submitted, and nothing is persisted.
        var identifierExists = await tenantService.IdentifierExistsAsync(request.Identifier, cancellationToken: cancellationToken);
        if (identifierExists)
        {
            ThrowError(x => x.Identifier, ITenantService.DuplicateIdentifierMessage, ErrorCodes.TenantIdentifierAlreadyExists);
        }

        var requestMapper = new TenantOnboardRequestMapper();
        var entity = requestMapper.Map(request);
        // Naming the caller as the first member is the whole difference between this and a tenant
        // created from the platform: the creation path persists the tenant active, provisions its
        // administrator role, gives the caller an active membership and assigns them that role, all
        // in one transaction, so the caller never holds a half-provisioned tenant. The creating
        // account and the creation time are stamped centrally on save.
        entity = await tenantService.CreateAsync(entity, creatorUserId, cancellationToken);

        // The session is re-established rather than ended: the caller stays signed in and enters no
        // credentials, while the principal, the token pair and the refresh-token row are all rebuilt
        // around the new tenant, which is what makes it their active tenant from the next request on.
        // The roles and permissions it embeds are recomputed from the membership and the assignments
        // just written, so they are the new tenant's own and carry nothing from the platform tier.
        var session = await tenantAuthorizationService.ReissueSessionAsync(creatorUserId, entity.Id, cancellationToken);

        var responseMapper = new TenantOnboardResponseMapper();
        var response = responseMapper.Map(entity);
        response.Session = session;

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for self-service tenant creation, carrying the display name and the url-safe
/// identifier of the tenant being created and nothing else: the creator is the authenticated caller
/// rather than a value in the payload, and lifecycle status, audit values and the system-created
/// flag are never a caller's to supply.
/// </summary>
public sealed class TenantOnboardRequest
{
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules for a self-service onboarding request. They are the shared tenant naming
/// rules, the very same extensions the platform create and update surfaces apply, so a name or an
/// identifier accepted from one surface is accepted from the other and every failure names the
/// offending field.
/// </summary>
sealed class TenantOnboardValidator : Validator<TenantOnboardRequest>
{
    public TenantOnboardValidator()
    {
        RuleFor(x => x.Name).NotEmpty().TenantName();
        RuleFor(x => x.Identifier).NotEmpty().TenantIdentifier();
    }
}

/// <summary>
/// Response payload returned after a successful onboarding, echoing the new tenant's assigned
/// identity, the name and identifier as they were stored, the normalized identifier maintained
/// beside them and the state the tenant was persisted in, together with the session material that
/// carries the caller into it.
/// </summary>
public sealed class TenantOnboardResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; set; } = null!;
    public TenantStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the re-established session, bound to the tenant just created. A JWT client
    /// replaces its token pair with this one; a cookie-authenticated client ignores it, its cookie
    /// having been rewritten already.
    /// </summary>
    public TenantSessionDto Session { get; set; } = null!;
}

/// <summary>
/// This mapper that projects a <see cref="TenantOnboardRequest"/> into a <see cref="Tenant"/> entity.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class TenantOnboardRequestMapper
{
    public partial Tenant Map(TenantOnboardRequest request);
}

/// <summary>
/// This mapper that projects the created <see cref="Tenant"/> entity into a
/// <see cref="TenantOnboardResponse"/>. The session is not the tenant's to describe, so it is left
/// to the endpoint to assign from the re-established session once the tenant exists.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantOnboardResponseMapper
{
    [MapperIgnoreTarget(nameof(TenantOnboardResponse.Session))]
    public partial TenantOnboardResponse Map(Tenant entity);
}