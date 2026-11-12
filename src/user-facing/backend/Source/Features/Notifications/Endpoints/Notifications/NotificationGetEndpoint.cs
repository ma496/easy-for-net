using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;

namespace Backend.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// GET endpoint that returns a single notification by id, resolving its per-user read state.
/// </summary>
/// <remarks>
/// The notification is looked for among the ones the caller can see, which is the same set the list
/// answers from: those addressed to them and the all-users broadcasts.
/// </remarks>
sealed class NotificationGetEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService) : Endpoint<NotificationGetRequest, NotificationGetResponse>
{
    public override void Configure()
    {
        Get("{id}");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(NotificationGetRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            await Send.UnauthorizedAsync(cancellationToken);
            return;
        }

        // The read state comes from the same WithReadState the list and the unread count use, so the row
        // reads as read or unread here exactly as it does there.
        var query = dbContext.Notifications
            .AsNoTracking()
            .VisibleTo(userId.Value)
            .Where(x => x.Id == request.Id)
            .WithReadState(dbContext, userId.Value);

        var notification = await NotificationGetResponseMapper.ProjectTo(query)
            .FirstOrDefaultAsync(cancellationToken);

        if (notification == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.ResponseAsync(notification, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload containing the identifier of the notification to fetch.
/// </summary>
sealed class NotificationGetRequest : BaseDto<Guid>
{
}

public sealed class NotificationGetResponse : AuditableDto<Guid>
{
    public NotificationType Type { get; set; }
    public string TitleKey { get; set; } = null!;
    public string MessageKey { get; set; } = null!;
    public bool IsRead { get; set; }
    public string? Group { get; set; }
    public string? Metadata { get; set; }

    public Guid? UserId { get; set; }
}

/// <summary>
/// Mapper that projects notifications paired with the caller's read state into <see cref="NotificationGetResponse"/>
/// DTOs: the notification's own members, and <c>IsRead</c> from the resolved read state rather than the row's flag.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
static partial class NotificationGetResponseMapper
{
    public static partial IQueryable<NotificationGetResponse> ProjectTo(IQueryable<NotificationWithReadState> query);

    [MapNestedProperties(nameof(NotificationWithReadState.Notification))]
    private static partial NotificationGetResponse Map(NotificationWithReadState source);
}
