namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /tenants/{tenantId}/members/seats</c> to report how many seats the
/// tenant named has taken and how many its plan allows, so the membership screen can stop offering to
/// add a member the API would refuse.
/// </summary>
/// <remarks>
/// Standing is settled exactly as <see cref="TenantMemberListEndpoint"/> settles it - a platform account
/// in platform scope, or a caller holding <see cref="Allow.TenantMember_View"/> inside the tenant named -
/// because the numbers describe the same members that surface lists. They are
/// <see cref="ITenantMembershipService.GetSeatsAsync"/>, the reading the limit is enforced against.
/// </remarks>
sealed class TenantMemberSeatsEndpoint(ICurrentUserService currentUserService,
                                       ITenantContext tenantContext,
                                       ITenantAuthorizationService tenantAuthorizationService,
                                       ITenantMembershipService tenantMembershipService) : Endpoint<TenantMemberSeatsRequest, TenantMemberSeatsResponse>
{
    /// <summary>
    /// The refusal reported to a caller with no standing in the tenant addressed - the same answer
    /// whether that tenant exists or not.
    /// </summary>
    private const string NotTenantMemberMessage = "Caller may not view the members of this tenant";

    public override void Configure()
    {
        Get("{tenantId}/members/seats");
        Group<TenantsGroup>();
        Permissions(Allow.TenantMember_View);
    }

    public override async Task HandleAsync(TenantMemberSeatsRequest request, CancellationToken cancellationToken)
    {
        var platformAdministration = currentUserService.IsPlatform() && tenantContext.IsPlatformScope();
        if (!platformAdministration)
        {
            var callerId = currentUserService.GetCurrentUserId();
            if (callerId is not { } caller
                || !await tenantAuthorizationService.HoldsTenantPermissionAsync(caller, request.TenantId, Allow.TenantMember_View, cancellationToken))
            {
                ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
            }
        }

        var seats = await tenantMembershipService.GetSeatsAsync(request.TenantId, cancellationToken);

        await Send.ResponseAsync(new TenantMemberSeatsResponse { Used = seats.Used, Limit = seats.Limit },
                                 cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming the tenant whose seats are reported.
/// </summary>
sealed class TenantMemberSeatsRequest
{
    public Guid TenantId { get; set; }
}

/// <summary>
/// FluentValidation rules requiring the tenant to be named.
/// </summary>
sealed class TenantMemberSeatsValidator : Validator<TenantMemberSeatsRequest>
{
    public TenantMemberSeatsValidator()
    {
        RuleFor(request => request.TenantId).NotEmpty();
    }
}

/// <summary>
/// Response payload reporting the seats a tenant has taken and the number its plan allows.
/// </summary>
public sealed class TenantMemberSeatsResponse
{
    /// <summary>
    /// Gets or sets the number of accounts holding a seat. Platform accounts take none.
    /// </summary>
    public int Used { get; set; }

    /// <summary>
    /// Gets or sets the number of seats the plan allows, or <see langword="null"/> when no limit applies.
    /// </summary>
    public long? Limit { get; set; }
}
