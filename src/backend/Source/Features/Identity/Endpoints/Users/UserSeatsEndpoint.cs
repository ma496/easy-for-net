namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /users/seats</c> to report how many accounts the tenant being
/// acted in holds, and how many its plan allows, so the users screen can show usage and stop offering
/// to create an account the API would refuse.
/// </summary>
/// <remarks>
/// The numbers are <see cref="ITenantMembershipService.GetSeatsAsync"/>, the same reading the limit is
/// enforced against, so the two cannot disagree. A caller acting in no tenant is inside
/// nobody's plan: it is told how many accounts it may administer and that there is no limit.
/// </remarks>
sealed class UserSeatsEndpoint(IUserService userService,
                               ITenantMembershipService tenantMembershipService,
                               ITenantContext tenantContext) : EndpointWithoutRequest<UserSeatsResponse>
{
    public override void Configure()
    {
        Get("seats");
        Group<UsersGroup>();
        Permissions(Allow.User_View);
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.CurrentTenantId is not { } tenantId)
        {
            var used = await userService.TenantUsers().CountAsync(cancellationToken);
            await Send.ResponseAsync(new UserSeatsResponse { Used = used }, cancellation: cancellationToken);
            return;
        }

        var seats = await tenantMembershipService.GetSeatsAsync(tenantId, cancellationToken);
        var response = new UserSeatsResponse { Used = seats.Used, Limit = seats.Limit };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload reporting the seats a tenant has taken and the number its plan allows.
/// </summary>
public sealed class UserSeatsResponse
{
    /// <summary>
    /// Gets or sets the number of accounts holding a seat.
    /// </summary>
    public int Used { get; set; }

    /// <summary>
    /// Gets or sets the number of seats the plan allows, or <see langword="null"/> when no limit applies.
    /// </summary>
    public long? Limit { get; set; }
}
