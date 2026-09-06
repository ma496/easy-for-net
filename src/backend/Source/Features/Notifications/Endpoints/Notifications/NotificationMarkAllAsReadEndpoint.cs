namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// POST endpoint that marks all unread notifications as read for the current user.
/// </summary>
sealed class NotificationMarkAllAsReadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService) : EndpointWithoutRequest<NotificationMarkAllAsReadResponse>
{
    public override void Configure()
    {
        Post("mark-all-as-read");
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Notifications
            .Where(x => x.UserId == userId.Value && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true), cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO notifications."NotificationVisits" ("Id", "UserId", "VisitedAt", "NotificationId")
            SELECT gen_random_uuid(), {{userId.Value}}, NOW(), notification."Id"
            FROM notifications."Notifications" AS notification
            WHERE notification."UserId" IS NULL AND notification."IsDeleted" = FALSE
            ON CONFLICT ("NotificationId", "UserId") DO NOTHING
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await Send.ResponseAsync(new NotificationMarkAllAsReadResponse { Success = true, Message = "All notifications marked as read" }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload confirming that all notifications were marked as read.
/// </summary>
public sealed class NotificationMarkAllAsReadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}
