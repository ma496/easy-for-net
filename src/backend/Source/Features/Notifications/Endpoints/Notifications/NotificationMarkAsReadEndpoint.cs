namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Base.Dto;
using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// POST endpoint that marks a single notification as read for the current user.
/// </summary>
/// <remarks>
/// The notification is sought among the ones the caller can see while acting in the active tenant, so a
/// notification of another tenant answers as one that does not exist, while a platform-wide notification
/// is reachable from whichever tenant the caller acts in - which is what lets a broadcast be marked read
/// by one of its recipients.
/// </remarks>
sealed class NotificationMarkAsReadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, ITenantContext tenantContext) : Endpoint<NotificationMarkAsReadRequest, NotificationMarkAsReadResponse>
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

        // Reading the active tenant here rather than leaning on the query filter is what lets the
        // platform-wide notification back in: the filter alone would hide every row naming no tenant.
        var activeTenantId = tenantContext.CurrentTenantId;

        var notification = await dbContext.Notifications
            .AcrossAllTenants()
            .VisibleTo(userId.Value, activeTenantId)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (notification == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        if (notification.UserId == null)
        {
            var isVisit = await dbContext.NotificationVisits
                .AnyAsync(v => v.NotificationId == notification.Id && v.UserId == userId.Value, cancellationToken);
            if (!isVisit)
            {
                dbContext.NotificationVisits.Add(new NotificationVisit
                {
                    NotificationId = notification.Id,
                    UserId = userId.Value,
                    VisitedAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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