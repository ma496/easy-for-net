namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Base.Dto;
using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// GET endpoint that returns a paged, filterable list of the notifications the current user can see while
/// acting in the active tenant, resolving per-user read state for the notifications addressed to an audience
/// rather than to one user. Three addressing modes reach the caller: the notifications of the active tenant
/// addressed to them personally, the notifications addressed to every member of that tenant, and the
/// platform-wide notifications, which name no tenant and therefore stay visible whichever tenant the caller
/// is acting in. Notifications raised in the caller's other tenants are not listed.
/// </summary>
sealed class NotificationListEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, ITenantContext tenantContext) : Endpoint<NotificationListRequest, NotificationListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<NotificationsGroup>();
    }

    public override async Task HandleAsync(NotificationListRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId == null)
        {
            return;
        }

        // Reading the active tenant here rather than leaning on the query filter is what lets the
        // platform-wide notifications back in: the filter alone would hide every row naming no tenant.
        // With no scope established this throws instead of listing rows the caller is not acting for.
        var activeTenantId = tenantContext.CurrentTenantId;

        // The tenant restriction is relaxed by name and then narrowed straight back down to the two
        // audiences the caller belongs to, so a notification of another tenant is unreachable here even
        // though the filter is off. The soft-delete filter stays in force.
        var query = dbContext.Notifications
            .AsNoTracking()
            .AcrossAllTenants()
            .VisibleTo(userId.Value, activeTenantId);

        if (request.IsRead == true)
        {
            query = query.Where(x => x.UserId == null
                ? dbContext.NotificationVisits.Any(v => v.UserId == userId.Value && v.NotificationId == x.Id)
                : x.IsRead);
        }
        else if (request.IsRead == false)
        {
            query = query.Where(x => x.UserId == null
                ? !dbContext.NotificationVisits.Any(v => v.UserId == userId.Value && v.NotificationId == x.Id)
                : !x.IsRead);
        }

        if (!string.IsNullOrWhiteSpace(request.Group))
        {
            query = query.Where(x => x.Group == request.Group);
        }

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.TitleKey, $"%{search}%") ||
                EF.Functions.ILike(x.MessageKey, $"%{search}%") ||
                x.Type.ToString().ToLower().Contains(search.ToLower()));
        }

        var total = await query.CountAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.SortField))
        {
            query = query
                .OrderBy(x => x.UserId == null
                    ? dbContext.NotificationVisits.Any(v => v.UserId == userId.Value && v.NotificationId == x.Id)
                    : x.IsRead)
                .ThenByDescending(x => x.CreatedAt);
        }

        query = query.Process(request, applyDefaultOrdering: false);

        var notifications = await query
            .Select(notification => new NotificationListDto
            {
                Id = notification.Id,
                CreatedAt = notification.CreatedAt,
                CreatedBy = notification.CreatedBy,
                UpdatedAt = notification.UpdatedAt,
                UpdatedBy = notification.UpdatedBy,
                Type = notification.Type,
                TitleKey = notification.TitleKey,
                MessageKey = notification.MessageKey,
                IsRead = notification.UserId == null
                    ? dbContext.NotificationVisits.Any(visit => visit.UserId == userId.Value && visit.NotificationId == notification.Id)
                    : notification.IsRead,
                Group = notification.Group,
                Metadata = notification.Metadata,
                UserId = notification.UserId
            })
            .ToListAsync(cancellationToken);

        await Send.ResponseAsync(new NotificationListResponse
        {
            Items = notifications,
            Total = total
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for listing notifications, supporting pagination, read-state filtering,
/// group filtering, and free-text search.
/// </summary>
sealed class NotificationListRequest : ListRequestDto<Guid>
{
    public bool? IsRead { get; set; }
    public string? Group { get; set; }
}

/// <summary>
/// Validator for <see cref="NotificationListRequest"/> that applies the standard list request rules.
/// </summary>
sealed class NotificationListValidator : Validator<NotificationListRequest>
{
    public NotificationListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Id", "Type", "TitleKey", "MessageKey", "Group", "CreatedAt", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>
/// Paged response containing notification list items and the total count.
/// </summary>
public sealed class NotificationListResponse : ListDto<NotificationListDto>
{
}

/// <summary>
/// DTO representing a single notification in list responses.
/// </summary>
public sealed class NotificationListDto : AuditableDto<Guid>
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
/// Mapper that projects a <see cref="Notification"/> query into <see cref="NotificationListDto"/> DTOs.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class NotificationListDtoMapper
{
    public static partial IQueryable<NotificationListDto> ProjectTo(IQueryable<Notification> query);

    private static partial NotificationListDto Map(Notification entity);
}
