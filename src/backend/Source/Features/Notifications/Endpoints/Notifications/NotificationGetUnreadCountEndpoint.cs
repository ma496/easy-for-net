namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;

/// <summary>
/// GET endpoint that returns the number of unread notifications the current user can see in the active
/// scope. The count is the unread part of exactly the set <c>NotificationListEndpoint</c> would list: the
/// caller's own unread notifications raised in that scope, the notifications addressed to every member of
/// the tenant they have not visited, and the platform-wide notifications they have not visited. Personal
/// notifications raised in another scope are never counted, so the badge cannot advertise a notification
/// the list will not show.
/// </summary>
sealed class NotificationGetUnreadCountEndpoint(ICurrentUserService currentUserService, INotificationService notificationService) : EndpointWithoutRequest<NotificationGetUnreadCountResponse>
{
    public override void Configure()
    {
        Get("unread-count");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        // The scope narrowing lives in the service: it reads the active scope itself and counts only
        // the rows the list endpoint would show, through the same VisibleTo the list reads through.
        var count = await notificationService.GetUnreadCountAsync(userId.Value, cancellationToken);

        await Send.ResponseAsync(new NotificationGetUnreadCountResponse { Count = count }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload containing the unread notification count for the current user in the active scope.
/// </summary>
public sealed class NotificationGetUnreadCountResponse
{
    public int Count { get; set; }
}