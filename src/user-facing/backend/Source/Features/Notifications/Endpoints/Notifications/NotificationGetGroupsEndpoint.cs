using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// GET endpoint that returns the distinct set of notification group names used for filtering in the UI.
/// </summary>
sealed class NotificationGetGroupsEndpoint(AppDbContext dbContext,
                                           ICurrentUserService currentUserService)
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

        // The filter options are drawn from the very set the list shows, through the same VisibleTo.
        var groups = await dbContext.Notifications
            .AsNoTracking()
            .VisibleTo(userId.Value)
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
