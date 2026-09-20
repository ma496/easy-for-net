namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// GET endpoint that returns the distinct set of notification group names used for filtering in the UI.
/// </summary>
sealed class NotificationGetGroupsEndpoint(AppDbContext dbContext,
                                           ICurrentUserService currentUserService,
                                           ITenantContext tenantContext)
    : EndpointWithoutRequest<NotificationGetGroupsResponse>
{
    public override void Configure()
    {
        Get("groups");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (!userId.HasValue)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        // The filter options have to be drawn from the very set the list shows, so the visibility rule
        // is the same one every other notification surface uses: relax the tenant restriction by name
        // and narrow straight back down with VisibleTo. Reading through the tenant filter instead would
        // leave out every platform-wide notification - those name no tenant, so the filter can never
        // match them - and the list would show a group the filter did not offer.
        var activeTenantId = tenantContext.CurrentTenantId;

        var groups = await dbContext.Notifications
            .AsNoTracking()
            .AcrossAllTenants()
            .VisibleTo(userId.Value, activeTenantId)
            .Where(x => x.Group != null)
            .Select(x => x.Group!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        await Send.ResponseAsync(new NotificationGetGroupsResponse { Groups = groups }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload containing the list of distinct notification group names.
/// </summary>
public sealed class NotificationGetGroupsResponse
{
    public List<string> Groups { get; set; } = [];
}
