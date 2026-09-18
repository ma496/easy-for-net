namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/{tenantId}/members</c> to make an account that
/// already exists a member of a tenant, holding exactly the roles of that tenant the request names.
/// </summary>
/// <remarks>
/// Marked <see cref="AllowNoTenantAttribute"/> because the tenant being administered is the one
/// named in the route rather than the one the session acts in: a platform administrator adds members
/// to a tenant they are not working in, and a tenant administrator whose session is in that tenant
/// reaches the same surface. The route id is therefore never a way to choose the acting tenant - it
/// is authorized here, explicitly, before anything is read.
/// <para>
/// The guards run in a fixed order, and the order is part of the contract. Standing is settled
/// first, so a caller who neither holds platform administration nor holds this permission inside the
/// tenant named is refused identically whether that tenant exists or not, and the surface discloses
/// nothing about tenants that are none of their business. Only then is the tenant looked up, then its
/// lifecycle state, then the account, then the account's standing in the tenant, then the roles asked
/// for.
/// </para>
/// <para>
/// Whether the account was ever a member of this tenant before is not asked: a removed membership is
/// soft-deleted and so nobody's membership, which is what lets an account rejoin a tenant it once
/// left and take exactly the roles this request names, with nothing inherited from the membership it
/// held before. Memberships of other tenants are neither read nor written, so the account's standing
/// everywhere else survives untouched.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TenantMemberAddEndpoint(ITenantService tenantService,
                                     ITenantMembershipService tenantMembershipService,
                                     ITenantAuthorizationService tenantAuthorizationService,
                                     ICurrentUserService currentUserService) : Endpoint<TenantMemberAddRequest, TenantMemberAddResponse>
{
    /// <summary>
    /// The refusal reported to a caller with no standing in the tenant named. It is deliberately the
    /// same answer for a tenant that does not exist, so the membership surface cannot be used to
    /// discover which tenants there are.
    /// </summary>
    private const string NotTenantMemberMessage = "Caller may not administer the members of this tenant";

    /// <summary>
    /// The refusal reported for a tenant the caller may not act on. Absent, deleted and invisible are
    /// deliberately one message and one code.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    /// <summary>
    /// The refusal reported when the tenant named is suspended. A suspended tenant is out of service
    /// rather than gone, so nothing inside it is changed while it is - its membership included.
    /// </summary>
    private const string TenantSuspendedMessage = "Members cannot be added while the tenant is suspended";

    /// <summary>
    /// The refusal reported when the account named does not exist. A membership joins an existing
    /// account to a tenant; it never brings an account into being.
    /// </summary>
    private const string UserNotFoundMessage = "User not found";

    /// <summary>
    /// The refusal reported when a role named is not one of this tenant's own. Another tenant's role
    /// and a role that exists nowhere are one answer, for the same reason a tenant the caller cannot
    /// see reads as missing.
    /// </summary>
    private const string RoleNotFoundMessage = "Referenced record does not exist.";

    public override void Configure()
    {
        Post("{tenantId}/members");
        Group<TenantsGroup>();
        Permissions(Allow.TenantMember_Add);
    }

    public override async Task HandleAsync(TenantMemberAddRequest request, CancellationToken cancellationToken)
    {
        // Asked before the tenant is looked up at all. Membership of the tenant is not what authorizes
        // this: the permission claims the request carries were minted for the tenant the session is
        // acting in, and the tenant being administered is the one in the route, which may be another one
        // entirely - so the permission is read for the tenant named in the route instead. Platform
        // administration is a claim test rather than a role-name test, because any tenant may define a
        // role of any name.
        if (!currentUserService.HasPermission(Allow.Platform_Administration))
        {
            var callerId = currentUserService.GetCurrentUserId();
            if (callerId is not { } callerUserId
                || !await tenantAuthorizationService.HoldsTenantPermissionAsync(callerUserId, request.TenantId, Allow.TenantMember_Add, cancellationToken))
            {
                ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
            }
        }

        // Read through the service, so a deleted tenant and one the caller has no standing in read as
        // missing here exactly as an absent one does. A caller who reached this line already has
        // standing in the tenant, so in practice only a platform administrator's route id fails it.
        var tenant = await tenantService.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            ThrowError(TenantSuspendedMessage, ErrorCodes.TenantSuspended);
        }

        // The account is owned by the identity slice and is only ever asked about through the one
        // contract that slice publishes, so no account type is named on this surface.
        var userExists = await tenantAuthorizationService.UserExistsAsync(request.UserId, cancellationToken);
        if (!userExists)
        {
            ThrowError(x => x.UserId, UserNotFoundMessage, ErrorCodes.UserNotFound);
        }

        // Only a membership that stands right now makes the account a member already: one that was
        // removed is soft-deleted and so is absent from this question, which is what lets a former
        // member be added back rather than refused over a membership that no longer exists.
        var alreadyMember = await tenantMembershipService.IsMemberAsync(request.TenantId, request.UserId, cancellationToken);
        if (alreadyMember)
        {
            ThrowError(x => x.UserId, ITenantMembershipService.DuplicateMembershipMessage, ErrorCodes.DuplicateTenantMembership);
        }

        // Checked before anything is written, so a request naming another tenant's role creates no
        // membership at all rather than one holding part of the set it asked for.
        var rolesBelongToTenant = await tenantAuthorizationService.AllRolesBelongToTenantAsync(request.TenantId, request.Roles, cancellationToken);
        if (!rolesBelongToTenant)
        {
            ThrowError(x => x.Roles, RoleNotFoundMessage, ErrorCodes.ReferencedRecordNotFound);
        }

        // The roles actually granted are reported rather than the ones asked for: a tenant gaining its
        // first member also gives that member the tenant's system-created administrator role, so that
        // every tenant is administrable from inside it from the moment anybody is in it.
        var result = await tenantMembershipService.AddAsync(request.TenantId, request.UserId, request.Roles, cancellationToken);

        var response = new TenantMemberAddResponse
        {
            Id = result.Membership.Id,
            TenantId = request.TenantId,
            UserId = request.UserId,
            Roles = result.RoleIds
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant to add a member to, the existing account being added and the
/// roles of that tenant the new member is to hold. The tenant travels in the route: it names the
/// tenant being administered and never the tenant the request acts in.
/// </summary>
public sealed class TenantMemberAddRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public List<Guid> Roles { get; set; } = [];
}

/// <summary>
/// FluentValidation rules ensuring an add-member request names a tenant, names an account and
/// carries a role set in which no entry is empty, so that every failure names the offending field.
/// An empty set is accepted: a member holding no role is a member who may do nothing in the tenant.
/// </summary>
sealed class TenantMemberAddValidator : Validator<TenantMemberAddRequest>
{
    public TenantMemberAddValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles).NotEmpty();
    }
}

/// <summary>
/// Response payload reporting the membership that was created, the pair it joins and the roles the
/// new member actually holds in that tenant - which is the set asked for, plus the tenant's
/// administrator role when this member is the tenant's first.
/// </summary>
public sealed class TenantMemberAddResponse : BaseDto<Guid>
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public List<Guid> Roles { get; set; } = [];
}