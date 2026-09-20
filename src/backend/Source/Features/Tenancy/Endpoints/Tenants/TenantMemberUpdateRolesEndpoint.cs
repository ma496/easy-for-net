namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>PUT /tenants/{tenantId}/members/{userId}/roles</c> to replace what a
/// member may do inside one tenant with exactly the roles supplied - the set given is the set the
/// member ends up holding there, so a role left out of it is withdrawn and a role named twice is
/// granted once.
/// </summary>
/// <remarks>
/// The tenant being administered is addressed by route rather than taken from the session, so this
/// endpoint needs no active tenant and authorizes that tenant itself: a caller who neither holds
/// a platform account in platform scope nor holds this permission inside the tenant addressed is refused before
/// the tenant is so much as looked for, so this surface cannot be used to discover which tenants
/// exist. Standing in the tenant the session happens to be acting in confers nothing here.
/// <para>
/// Only the member's assignments inside this one tenant are rewritten. The same account's memberships
/// of other tenants, and the roles it holds in them, are outside everything this writes, which is
/// what lets an account belong to several tenants with different standing in each. The change reaches
/// the member's live sessions on their next request - what a session may do is recomputed from the
/// assignments each time - so they are neither signed out nor asked to sign in again for it to take
/// effect.
/// </para>
/// <para>
/// Two administrators re-roling the same member at once cannot interleave into a set neither of them
/// asked for: the replacement writes the membership row, whose <c>xmin</c> concurrency token makes the
/// later save fail rather than overwrite a set its caller never saw, and that failure is reported as
/// <see cref="ErrorCodes.ConcurrentModification"/> so the loser can re-read and decide again.
/// </para>
/// </remarks>
sealed class TenantMemberUpdateRolesEndpoint(ITenantService tenantService,
                                             ITenantMembershipService tenantMembershipService,
                                             ITenantAuthorizationService tenantAuthorizationService,
                                             ICurrentUserService currentUserService,
                                             ITenantContext tenantContext)
    : Endpoint<TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>
{
    /// <summary>
    /// The refusal reported to a caller with no standing in the tenant addressed. It is raised before
    /// the tenant is read, so an absent tenant and somebody else's tenant read the same way here.
    /// </summary>
    private const string NotTenantMemberMessage = "Caller may not administer the members of this tenant";

    /// <summary>
    /// The refusal reported for a tenant the caller may not act on. Absent, deleted and invisible are
    /// deliberately one message and one code, so the response cannot reveal which of the three it was.
    /// </summary>
    private const string TenantNotFoundMessage = "Tenant not found";

    /// <summary>
    /// The refusal reported when the tenant is suspended. Membership is administered only in a tenant
    /// that is in service, so the change waits until the tenant is reactivated.
    /// </summary>
    private const string TenantSuspendedMessage = "Tenant is suspended";

    /// <summary>
    /// The refusal reported when a role named does not belong to the tenant being administered - a
    /// role of another tenant, or a role that exists nowhere. Raised against the roles field, so the
    /// caller is told which value was refused.
    /// </summary>
    private const string RolesOutsideTenantMessage = "One or more roles do not belong to this tenant";

    /// <summary>
    /// The refusal reported when the member's assignments were replaced by another request while this
    /// one was replacing them, so that no part of either request is left half-applied.
    /// </summary>
    private const string ConcurrentModificationMessage = "Member roles were changed by another request";

    public override void Configure()
    {
        Put("{tenantId}/members/{userId}/roles");
        Group<TenantsGroup>();
        Permissions(Allow.TenantMember_UpdateRoles);
    }

    public override async Task HandleAsync(TenantMemberUpdateRolesRequest request, CancellationToken cancellationToken)
    {
        // Standing first, before the tenant is looked for at all. A caller who may not administer the
        // tenant addressed gets the same answer whether that tenant exists or not, so this surface
        // discloses nothing about tenants they have no part in.
        //
        // Membership of the tenant is not what authorizes this: the permission claims the request
        // carries were minted for the tenant the session is acting in, and the tenant being
        // administered is the one in the route, which may be another one entirely. An account can be
        // an administrator of one tenant and an ordinary member of the next, so the permission is read
        // for the tenant named in the route rather than taken from the claims. Administering any tenant
        // belongs to platform scope, so a platform account that has entered a tenant is read for that
        // route tenant like anybody else.
        var callerId = currentUserService.GetCurrentUserId();
        if (!(currentUserService.IsPlatform() && tenantContext.IsPlatformScope()) &&
            (callerId is not { } callerUserId ||
             !await tenantAuthorizationService.HoldsTenantPermissionAsync(callerUserId, request.TenantId, Allow.TenantMember_UpdateRoles, cancellationToken)))
        {
            ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
        }

        // Read through the service, so a tenant that never existed, one that has been deleted and one
        // a platform administrator may see but this caller may not are all the same answer.
        var tenant = await tenantService.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            ThrowError(TenantNotFoundMessage, ErrorCodes.TenantNotFound);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            ThrowError(TenantSuspendedMessage, ErrorCodes.TenantSuspended);
        }

        // A membership is a record inside a tenant, so an account holding none there - never added, or
        // since removed - is reported exactly as a missing record rather than as a business refusal.
        var membership = await tenantMembershipService.GetAsync(request.TenantId, request.UserId, cancellationToken);
        if (membership == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        // Duplicates collapse here rather than at the database, so the set persisted, the set counted
        // against the tenant's roles and the set reported back are one and the same set.
        var roleIds = request.Roles.Distinct().ToList();

        // Checked before anything is written, so a request naming another tenant's role - or a role
        // that exists nowhere - persists no part of itself.
        if (!await tenantAuthorizationService.AllRolesBelongToTenantAsync(request.TenantId, roleIds, cancellationToken))
        {
            ThrowError(x => x.Roles, RolesOutsideTenantMessage, ErrorCodes.ReferencedRecordNotFound);
        }

        // The replacement, the membership row it touches and the last-administrator count all happen in
        // one transaction inside the service, so the tenant is never briefly left unadministered and a
        // refused change leaves the member's roles exactly as they stood before the request.
        try
        {
            var outcome = await tenantMembershipService.ReplaceRolesAsync(request.TenantId, request.UserId, roleIds, cancellationToken);

            // The membership was removed between the read above and the replacement: the answer is the
            // one that read would have given had it happened a moment later.
            if (outcome == TenantMembershipChangeOutcome.MembershipNotFound)
            {
                await Send.NotFoundAsync(cancellationToken);
                return;
            }

            // A tenant is never left with nobody able to administer it, whether the administration
            // being withdrawn is this member's own or the last one held anywhere in the tenant.
            if (outcome == TenantMembershipChangeOutcome.LastTenantAdministrator)
            {
                ThrowError(ITenantMembershipService.LastAdministratorMessage, ErrorCodes.LastTenantAdministrator);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            // The membership's concurrency token was stale, so another request replaced this member's
            // roles while this one was working. Nothing of this request was persisted, and the caller is
            // told to look again rather than left believing a set nobody ever saw whole is in force.
            ThrowError(ConcurrentModificationMessage, ErrorCodes.ConcurrentModification);
        }

        await Send.ResponseAsync(new()
        {
            Id = membership.Id,
            TenantId = request.TenantId,
            UserId = request.UserId,
            Roles = roleIds
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant and the member from the route, and carrying the complete set of
/// roles that member is to hold inside that tenant. An empty set is a valid request: it leaves the
/// member in the tenant holding no role there.
/// </summary>
public sealed class TenantMemberUpdateRolesRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public List<Guid> Roles { get; set; } = [];
}

/// <summary>
/// FluentValidation rules for a role-replacement request: both route identities must be supplied, and
/// the roles collection must be present - though it may be empty - with no empty identity inside it.
/// </summary>
sealed class TenantMemberUpdateRolesValidator : Validator<TenantMemberUpdateRolesRequest>
{
    public TenantMemberUpdateRolesValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles).NotEmpty();
    }
}

/// <summary>
/// Response payload echoing the membership that was re-roled and the roles the member now holds in
/// that tenant, so the caller can confirm the set that took effect without reading the member again.
/// The id is the membership's identity, not the account's.
/// </summary>
public sealed class TenantMemberUpdateRolesResponse : BaseDto<Guid>
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public List<Guid> Roles { get; set; } = [];
}