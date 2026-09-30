namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Base.Dto;
using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// GET endpoint that returns a paged, filterable list of the notifications the current user can see in the
/// active scope, resolving per-user read state for the notifications addressed to an audience rather than to
/// one user. Three addressing modes reach the caller: the notifications of the active scope - the tenant, or
/// platform scope - addressed to them personally, the notifications addressed to every member of the tenant,
/// and the platform-wide notifications, which name neither a tenant nor a user and therefore stay visible
/// in every scope. Personal notifications raised in another scope are not listed.
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

        // Read state, for the filter, the default ordering and the projection alike, is the one
        // WithReadState resolves - the row's own flag for a personal notification, the caller's visit or
        // read cursor for an audience one - so the list cannot disagree with the unread count.
        var withReadState = query.WithReadState(dbContext, userId.Value);

        if (request.IsRead is { } isRead)
        {
            withReadState = withReadState.Where(x => x.IsRead == isRead);
        }

        var total = await withReadState.CountAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.SortField))
        {
            withReadState = withReadState
                .OrderBy(x => x.IsRead)
                .ThenByDescending(x => x.Notification.CreatedAt);
        }

        // Sorting by a whitelisted field and paging are applied to the notifications themselves, which is
        // what Process works over; the default ordering above survives it because Process adds none of its
        // own. The page is then paired with its read state again for the projection.
        var page = withReadState
            .Select(x => x.Notification)
            .Process(request, applyDefaultOrdering: false)
            .WithReadState(dbContext, userId.Value);

        var notifications = await NotificationListDtoMapper.ProjectTo(page)
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
/// Mapper that projects notifications paired with the caller's read state into <see cref="NotificationListDto"/>
/// DTOs: the notification's own members, and <c>IsRead</c> from the resolved read state rather than the row's flag.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
static partial class NotificationListDtoMapper
{
    public static partial IQueryable<NotificationListDto> ProjectTo(IQueryable<NotificationWithReadState> query);

    [MapNestedProperties(nameof(NotificationWithReadState.Notification))]
    private static partial NotificationListDto Map(NotificationWithReadState source);
}
