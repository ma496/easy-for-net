namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;

/// <summary>
/// GET endpoint that returns the number of unread notifications the current user can see while acting in
/// the active tenant. The count is the unread part of exactly the set <c>NotificationListEndpoint</c> would
/// list: the caller's own unread notifications in the active tenant, the notifications addressed to every
/// member of that tenant they have not visited, and the platform-wide notifications they have not visited.
/// Notifications raised in the caller's other tenants are never counted, so the badge cannot advertise a
/// notification the list will not show.
/// </summary>
[AllowPlatformNoTenant]
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

        // The tenant narrowing lives in the service: it reads the active scope itself and counts only
        // the rows the list endpoint would show. The list endpoint spells the same predicate out over
        // its own query, so the two are kept in step by hand and have to be changed together.
        var count = await notificationService.GetUnreadCountAsync(userId.Value, cancellationToken);

        await Send.ResponseAsync(new NotificationGetUnreadCountResponse { Count = count }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload containing the unread notification count for the current user in the active tenant.
/// </summary>
public sealed class NotificationGetUnreadCountResponse
{
    public int Count { get; set; }
}