using Backend.Base.Dto;
using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Push;

namespace Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// POST endpoint that marks a single notification as read for the current user.
/// </summary>
/// <remarks>
/// The notification is sought among the ones the caller can see, so another user's answers as one that
/// does not exist. A personal notification's own flag is set; for a broadcast the caller's visit row is
/// upserted as read.
/// </remarks>
sealed class NotificationMarkAsReadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, INotificationPublisher publisher) : Endpoint<NotificationMarkAsReadRequest, NotificationMarkAsReadResponse>
{
    public override void Configure()
    {
        Post("{id}/mark-as-read");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(NotificationMarkAsReadRequest request, CancellationToken cancellationToken)
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
            // An audience notification is shared by all its readers, so the caller's read state lives in
            // their visit row, which overrides their read cursor either way. Upserting it keeps two
            // concurrent marks from colliding on the unique (NotificationId, UserId) index.
            await NotificationQueries.SetAudienceReadStateAsync(dbContext, notification.Id, userId.Value, isRead: true, cancellationToken);
        }
        else
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // The caller's other connections learn the new count; nobody else's changes.
        await publisher.PublishUnreadCountAsync(userId.Value, cancellationToken);

        await Send.ResponseAsync(new NotificationMarkAsReadResponse { Id = request.Id, Success = true, Message = "Notification marked as read" }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload containing the identifier of the notification to mark as read.
/// </summary>
sealed class NotificationMarkAsReadRequest : BaseDto<Guid>
{
}

/// <summary>
/// Validates that the <see cref="NotificationMarkAsReadRequest"/> has a non-empty notification id.
/// </summary>
sealed class NotificationMarkAsReadValidator : Validator<NotificationMarkAsReadRequest>
{
    public NotificationMarkAsReadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload confirming that a notification has been marked as read.
/// </summary>
public sealed class NotificationMarkAsReadResponse : BaseDto<Guid>
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}