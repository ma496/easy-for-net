namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /tenants/{tenantId}/members/{userId}</c> to revoke one
/// account's membership of one tenant, withdrawing with it every role that account held inside that
/// tenant and so its access to that tenant's data.
/// </summary>
/// <remarks>
/// The tenant being administered is addressed by route rather than taken from the session, so this
/// endpoint needs no active tenant and authorizes that tenant itself: a caller who neither holds
/// a platform account in platform scope nor holds this permission inside the tenant addressed is refused before
/// the tenant is so much as looked for, so this surface cannot be used to discover which tenants
/// exist. Standing in the tenant the session happens to be acting in confers nothing here.
/// <para>
/// Only this one membership is revoked. The user account survives, and so do its memberships of every
/// other tenant and the roles it holds in them - which is what lets an account belong to several
/// tenants and lose its place in one of them without losing the others. The membership row itself is
/// retained, soft-deleted, so the tenant keeps the record that the account once belonged to it while
/// every membership read stops finding it.
/// </para>
/// <para>
/// The removed member's live sessions lose access to this tenant on their next request and to nothing
/// else: no password is changed, no session is ended, and a session acting in another tenant they
/// still belong to carries on. Nothing is pushed to those sessions for that to happen - what a session
/// may do is recomputed from the membership and the role assignments on every request, and this
/// removes both.
/// </para>
/// <para>
/// A tenant is never left with nobody able to administer it: a removal that would withdraw the last
/// tenant-administration standing in the tenant is refused with
/// <see cref="ErrorCodes.LastTenantAdministrator"/> and writes nothing at all.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TenantMemberRemoveEndpoint(ITenantService tenantService,
                                        ITenantMembershipService tenantMembershipService,
                                        ITenantAuthorizationService tenantAuthorizationService,
                                        ICurrentUserService currentUserService,
                                        ITenantContext tenantContext)
    : Endpoint<TenantMemberRemoveRequest, TenantMemberRemoveResponse>
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

    public override void Configure()
    {
        Delete("{tenantId}/members/{userId}");
        Group<TenantsGroup>();
        Permissions(Allow.TenantMember_Remove);
    }

    public override async Task HandleAsync(TenantMemberRemoveRequest request, CancellationToken cancellationToken)
    {
        // Standing first, before the tenant is looked for at all. A caller who may not administer the
        // tenant addressed gets the same answer whether that tenant exists or not, so this surface
        // discloses nothing about tenants they have no part in.
        //
        // Membership of the tenant is not what authorizes this: the permission claims the request
        // carries were minted for the tenant the session is acting in, and the tenant being
        // administered is the one in the route, which may be another one entirely. The permission is
        // therefore read for the tenant named in the route. Administering any tenant belongs to platform
        // scope, so a platform account that has entered a tenant is read for that route tenant like
        // anybody else.
        var callerId = currentUserService.GetCurrentUserId();
        if (!(currentUserService.IsPlatform() && tenantContext.IsPlatformScope()) &&
            (callerId is not { } callerUserId ||
             !await tenantAuthorizationService.HoldsTenantPermissionAsync(callerUserId, request.TenantId, Allow.TenantMember_Remove, cancellationToken)))
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

        // Suspension is deliberately not a bar here. A suspended tenant is out of service for the work
        // done inside it, but who belongs to it is still administrable, so a member can be removed from
        // one rather than being left in place until it is reactivated.

        // The revocation, the withdrawal of the tenant's roles from the member and the
        // last-administrator count all happen in one transaction inside the service, so the tenant is
        // never briefly left unadministered and a refused removal leaves the membership exactly as it
        // stood before the request.
        var outcome = await tenantMembershipService.RemoveAsync(request.TenantId, request.UserId, cancellationToken);

        // A membership is a record inside a tenant, so an account holding none there - never added, or
        // already removed - is reported exactly as a missing record rather than as a business refusal.
        if (outcome == TenantMembershipChangeOutcome.MembershipNotFound)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        // Refused rather than corrected: the tenant would have been left with no member able to
        // administer it, and nothing of the removal was persisted.
        if (outcome == TenantMembershipChangeOutcome.LastTenantAdministrator)
        {
            ThrowError(ITenantMembershipService.LastAdministratorMessage, ErrorCodes.LastTenantAdministrator);
        }

        await Send.ResponseAsync(new() { Success = true }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming, from the route, the tenant the member is being removed from and the
/// account whose membership of it is being revoked.
/// </summary>
public sealed class TenantMemberRemoveRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
}

/// <summary>
/// FluentValidation rules for a member-removal request: both route identities must be supplied.
/// </summary>
sealed class TenantMemberRemoveValidator : Validator<TenantMemberRemoveRequest>
{
    public TenantMemberRemoveValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

/// <summary>
/// Response payload indicating the outcome of a member-removal attempt.
/// </summary>
public sealed class TenantMemberRemoveResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}