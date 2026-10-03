namespace Backend.Features.Tenancy.Endpoints.Tenants;

using System.Text.Json;
using Backend.ShareData.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /tenants/{tenantId}/members</c> to make an account that
/// already exists a member of a tenant, holding exactly the roles of that tenant the request names.
/// </summary>
/// <remarks>
/// Usable with no tenant established, because the tenant being administered is the one
/// named in the route rather than the one the session acts in: a platform administrator adds members
/// to a tenant they are not working in, and a tenant administrator whose session is in that tenant
/// reaches the same surface. The route id is therefore never a way to choose the acting tenant - it
/// is authorized here, explicitly, before anything is read.
/// <para>
/// The guards run in a fixed order, and the order is part of the contract. Standing is settled
/// first, so a caller who is neither a platform account in platform scope nor holds this permission inside the
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
sealed class TenantMemberAddEndpoint(ITenantService tenantService,
                                     ITenantMembershipService tenantMembershipService,
                                     ITenantAuthorizationService tenantAuthorizationService,
                                     ICurrentUserService currentUserService,
                                     ITenantContext tenantContext,
                                     INotificationService notificationService) : Endpoint<TenantMemberAddRequest, TenantMemberAddResponse>
{
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
        // entirely - so the permission is read for the tenant named in the route instead. Administering
        // any tenant belongs to platform scope, so a platform account that has entered a tenant is read
        // for that route tenant like anybody else.
        var platformAdministration = currentUserService.IsPlatform() && tenantContext.IsPlatformScope();
        if (!platformAdministration)
        {
            var callerId = currentUserService.GetCurrentUserId();
            if (callerId is not { } callerUserId
                || !await tenantAuthorizationService.HoldsTenantPermissionAsync(callerUserId, request.TenantId, Allow.TenantMember_Add, cancellationToken))
            {
                this.ThrowError(ErrorCodes.NotTenantMember);
            }
        }

        // Read through the service, so a deleted tenant and one the caller has no standing in read as
        // missing here exactly as an absent one does. A caller who reached this line already has
        // standing in the tenant, so in practice only a platform administrator's route id fails it.
        // Only the status checked below and the name the notification carries are read.
        var tenant = await tenantService.Tenants()
            .AsNoTracking()
            .Where(x => x.Id == request.TenantId)
            .Select(x => new { x.Status, x.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (tenant == null)
        {
            this.ThrowError(ErrorCodes.TenantNotFound);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            this.ThrowError(ErrorCodes.TenantSuspended);
        }

        // The account is owned by the identity slice and is only ever asked about through the one
        // contract that slice publishes, so no account type is named on this surface.
        // A platform account is administered from platform scope only, so a tenant's own administrators
        // cannot bring one into their tenant: to them it reads exactly as an account that does not exist.
        var userExists = await tenantAuthorizationService.UserExistsAsync(request.UserId, cancellationToken)
                         && (platformAdministration || !await tenantAuthorizationService.IsPlatformAccountAsync(request.UserId, cancellationToken));
        if (!userExists)
        {
            this.ThrowError(x => x.UserId, ErrorCodes.UserNotFound);
        }

        // Only a membership that stands right now makes the account a member already: one that was
        // removed is soft-deleted and so is absent from this question, which is what lets a former
        // member be added back rather than refused over a membership that no longer exists.
        var alreadyMember = await tenantMembershipService.IsMemberAsync(request.TenantId, request.UserId, cancellationToken);
        if (alreadyMember)
        {
            this.ThrowError(x => x.UserId, ErrorCodes.DuplicateTenantMembership);
        }

        // Checked before anything is written, so a request naming another tenant's role creates no
        // membership at all rather than one holding part of the set it asked for.
        var rolesBelongToTenant = await tenantAuthorizationService.AllRolesBelongToTenantAsync(request.TenantId, request.Roles, cancellationToken);
        if (!rolesBelongToTenant)
        {
            this.ThrowError(x => x.Roles, ErrorCodes.ReferencedRecordNotFound);
        }

        // The roles actually granted are reported rather than the ones asked for: a tenant gaining its
        // first member also gives that member the tenant's system-created administrator role, so that
        // every tenant is administrable from inside it from the moment anybody is in it.
        var result = await tenantMembershipService.AddAsync(request.TenantId, request.UserId, request.Roles, cancellationToken);

        // Raised in the tenant the account joined rather than the one the caller acts in - a platform
        // administrator adds members from platform scope - so the new member finds it once they act in
        // that tenant. The membership is already committed, so nothing unsaved is flushed in this scope.
        using (tenantContext.BeginTenant(request.TenantId))
        {
            await notificationService.NewUserNotificationAsync(request.UserId,
                                                               NotificationType.Info,
                                                               "notifications.tenantMemberAdded.title",
                                                               "notifications.tenantMemberAdded.message",
                                                               "system",
                                                               JsonSerializer.Serialize(new { tenantName = tenant.Name }),
                                                               cancellationToken);
        }

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
public sealed class TenantMemberAddResponse : BaseDto<Guid>, IHaveTenantDto
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public List<Guid> Roles { get; set; } = [];
}
