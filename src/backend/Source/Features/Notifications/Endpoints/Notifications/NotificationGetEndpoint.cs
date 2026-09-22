namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// GET endpoint that returns a single notification by id, resolving its per-user read state.
/// </summary>
/// <remarks>
/// The notification is looked for among the ones the caller can see while acting in the active tenant,
/// which is the same set the list answers from: the tenant's own notifications addressed to them or to
/// its whole membership, and the platform-wide ones. Both halves matter here - a notification of another
/// tenant is not the caller's to read, and a platform-wide one is, whichever tenant they act in.
/// </remarks>
sealed class NotificationGetEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, ITenantContext tenantContext) : Endpoint<NotificationGetRequest, NotificationGetResponse>
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

        // Reading the active tenant here rather than leaning on the query filter is what lets the
        // platform-wide notification back in: the filter alone would hide every row naming no tenant.
        var activeTenantId = tenantContext.CurrentTenantId;

        var query = dbContext.Notifications
            .AsNoTracking()
            .AcrossAllTenants()
            .VisibleTo(userId.Value, activeTenantId)
            .Where(x => x.Id == request.Id);

        var notification = await NotificationGetResponseMapper.ProjectTo(query)
            .FirstOrDefaultAsync(cancellationToken);

        if (notification == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        if (notification.UserId == null)
        {
            notification.IsRead = await dbContext.NotificationVisits
                .AnyAsync(v => v.NotificationId == notification.Id && v.UserId == userId.Value, cancellationToken);
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
/// Mapper that projects a <see cref="Notification"/> query into <see cref="NotificationGetResponse"/> DTOs.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class NotificationGetResponseMapper
{
    public static partial IQueryable<NotificationGetResponse> ProjectTo(IQueryable<Notification> query);

    private static partial NotificationGetResponse Map(Notification entity);
}
