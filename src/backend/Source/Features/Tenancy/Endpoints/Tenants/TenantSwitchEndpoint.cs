namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/switch</c> to make one of the tenants the caller
/// belongs to the tenant their session acts in, leaving them authenticated and asking for no
/// credentials.
/// </summary>
/// <remarks>
/// This is the only way an active tenant is chosen. The tenant a request acts in travels inside the
/// authenticated session and is never read off the request that acts in it, so a caller cannot reach
/// another tenant's rows by adding a tenant to a header, a query value or a payload: the tenant named
/// here is authorized once, written into a re-established session, and every later request takes it
/// from there. A caller holding memberships in more than one tenant therefore starts with no active
/// tenant at all and has every tenant-scoped operation refused until they select one through this
/// surface.
/// <para>
/// Usable with no tenant established for the same reason: this is the endpoint that
/// establishes a tenant, so requiring one would leave an account with several memberships unable to
/// pick any of them. It declares no permission either - which tenants a caller may act in is decided
/// by their memberships, not by anything a role grants them.
/// </para>
/// <para>
/// The platform tier is no exception: a platform account enters only a tenant it has been made a
/// member of, and inside it exercises the roles that membership carries there and none of its
/// platform roles. It leaves through <c>POST /tenants/exit</c>, which puts the session back into
/// platform scope and its platform permissions back with it.
/// </para>
/// <para>
/// The guards run in a fixed order, and the order is part of the contract. The tenant is read off the
/// set rather than through <see cref="ITenantService"/> on purpose: that service narrows to the
/// tenants the caller may see, which would report a tenant the caller merely does not belong to as
/// absent, and this surface has to tell the two apart. Existence is settled first, so an absent or
/// deleted tenant - including one a stale stored selection still names - is refused with
/// <see cref="ErrorCodes.TenantNotFound"/> and nothing further is disclosed about it; then the
/// caller's own standing, so a caller holding no active membership is refused with
/// <see cref="ErrorCodes.NotTenantMember"/> whatever they may do in the tenant they act in now; then
/// the tenant's lifecycle state, so a suspended tenant cannot be switched into and worked in.
/// </para>
/// <para>
/// What the re-established session may do is recomputed from the membership and the role assignments
/// as they stand at this moment, for the selected tenant alone. Nothing is copied from the session it
/// replaces, so authority the caller carried for the tenant they were acting in a moment ago reaches
/// neither this response nor the requests that follow it.
/// </para>
/// </remarks>
sealed class TenantSwitchEndpoint(AppDbContext dbContext,
                                  ITenantMembershipService tenantMembershipService,
                                  ITenantAuthorizationService tenantAuthorizationService,
                                  ICurrentUserService currentUserService) : Endpoint<TenantSwitchRequest, TenantSwitchResponse>
{
    /// <summary>
    /// The refusal reported for a tenant that does not exist or has been deleted. It carries no name
    /// and no detail, so the answer for a tenant that never existed and the answer for one that is
    /// gone are the same answer.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    /// <summary>
    /// The refusal reported when the caller holds no active membership in the tenant selected. Holding
    /// permissions - even every permission - in another tenant or on the platform is not standing in
    /// this one; only a membership of it is.
    /// </summary>
    private const string NotTenantMemberMessage = "Caller is not a member of this tenant";

    /// <summary>
    /// The refusal reported when the tenant selected is suspended. A suspended tenant is out of
    /// service rather than gone, so the caller keeps their session and may select another tenant they
    /// belong to instead.
    /// </summary>
    private const string TenantSuspendedMessage = "The tenant is suspended";

    public override void Configure()
    {
        Post("switch");
        Group<TenantsGroup>();
    }

    public override async Task HandleAsync(TenantSwitchRequest request, CancellationToken cancellationToken)
    {
        var callerId = currentUserService.GetCurrentUserId();
        if (callerId is not { } userId)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        // A tenant is the scope rather than something inside one, so this read is restricted by
        // nothing but the soft-delete filter: a deleted tenant is absent here exactly as a tenant that
        // never existed is, which is what makes the one selection failure indistinguishable from the
        // other.
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.TenantId, cancellationToken);
        if (tenant == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        // Asked of the membership rows as they stand now, and of nothing the caller's current session
        // carries: a membership that has been removed is soft-deleted and so keeps nobody inside a
        // tenant, and no permission granted anywhere else - the platform tier included - is standing here.
        var isMember = await tenantMembershipService.IsMemberAsync(tenant.Id, userId, cancellationToken);
        if (!isMember)
        {
            ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            ThrowError(TenantSuspendedMessage, ErrorCodes.TenantSuspended);
        }

        // Re-establishes the session rather than issuing a second one beside it: the cookie principal
        // is re-signed, a fresh access/refresh pair is issued and the selected tenant is recorded on
        // the refresh-token row, so neither this session nor a later refresh of it can act in the
        // tenant the caller was acting in before.
        var session = await tenantAuthorizationService.ReissueSessionAsync(userId, tenant.Id, cancellationToken);

        var response = new TenantSwitchResponse
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            Identifier = tenant.Identifier,
            Status = tenant.Status,
            Session = session
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant to act in from now on. It names a selection to authorize and
/// never the tenant this request itself acts in: the selection takes effect only through the session
/// this endpoint re-establishes, and the payload carries no credential of any kind.
/// </summary>
public sealed class TenantSwitchRequest
{
    public Guid TenantId { get; set; }
}

/// <summary>
/// Validates that the <see cref="TenantSwitchRequest"/> names a tenant.
/// </summary>
sealed class TenantSwitchValidator : Validator<TenantSwitchRequest>
{
    public TenantSwitchValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

/// <summary>
/// Response payload describing the tenant now active, together with the session material that carries
/// it. A client authenticating with tokens replaces its pair with the <see cref="TenantSessionDto"/>;
/// a cookie-authenticated client ignores it, because the re-signed cookie already carries the newly
/// selected tenant.
/// </summary>
public sealed class TenantSwitchResponse
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public TenantStatus Status { get; set; }
    public TenantSessionDto Session { get; set; } = null!;
}
