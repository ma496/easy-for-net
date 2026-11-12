using Backend.Base.Dto;
using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Push;

namespace Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// POST endpoint that marks a single notification as unread for the current user.
/// </summary>
/// <remarks>
/// The notification is sought among the ones the caller can see, so another user's answers as one that
/// does not exist. For a broadcast the caller's visit row is upserted as unread
/// rather than deleted, because an unread visit is what overrides a read cursor that already covers it.
/// </remarks>
sealed class NotificationMarkAsUnreadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, INotificationPublisher publisher) : Endpoint<NotificationMarkAsUnreadRequest, NotificationMarkAsUnreadResponse>
{
    public override void Configure()
    {
        Post("{id}/mark-as-unread");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(NotificationMarkAsUnreadRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        var notification = await dbContext.Notifications
            .VisibleTo(userId.Value)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (notification == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        if (notification.UserId == null)
        {
            // Removing the visit row would not be enough: a notification the caller's read cursor covers
            // would still read as read. An unread visit row overrides the cursor, so it is written instead.
            await NotificationQueries.SetAudienceReadStateAsync(dbContext, notification.Id, userId.Value, isRead: false, cancellationToken);
        }
        else
        {
            notification.IsRead = false;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // The caller's other connections learn the new count; nobody else's changes.
        await publisher.PublishUnreadCountAsync(userId.Value, cancellationToken);

        await Send.ResponseAsync(new NotificationMarkAsUnreadResponse { Id = request.Id, Success = true, Message = "Notification marked as unread" }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload containing the identifier of the notification to mark as unread.
/// </summary>
sealed class NotificationMarkAsUnreadRequest : BaseDto<Guid>
{
}

/// <summary>
/// Validates that the <see cref="NotificationMarkAsUnreadRequest"/> has a non-empty notification id.
/// </summary>
sealed class NotificationMarkAsUnreadValidator : Validator<NotificationMarkAsUnreadRequest>
{
    public NotificationMarkAsUnreadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload confirming that a notification has been marked as unread.
/// </summary>
public sealed class NotificationMarkAsUnreadResponse : BaseDto<Guid>
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}