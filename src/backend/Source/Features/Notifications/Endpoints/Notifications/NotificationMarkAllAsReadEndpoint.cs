namespace Backend.Features.Notifications.Endpoints.Notifications;

using Backend.Features.Identity.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// POST endpoint that marks every notification the current user can see while acting in the active tenant
/// as read: their own unread notifications of that tenant, the notifications addressed to the whole tenant,
/// and the platform-wide ones. Notifications raised in the caller's other tenants are left unread.
/// </summary>
/// <remarks>
/// Both halves below are bulk statements rather than per-record saves - one <c>ExecuteUpdate</c> and one
/// hand-written <c>INSERT ... ON CONFLICT</c> - so each carries its tenant restriction in a predicate of its
/// own. The hand-written statement is reached by no query filter at all, and the bulk update deliberately
/// relaxes the tenant filter so that the platform-wide rows stay reachable, which leaves the written
/// predicate as the only thing keeping another tenant's rows out of both. The two predicates together
/// describe exactly the set <c>INotificationService.GetUnreadCountAsync</c> counts, so the unread badge
/// reads zero afterwards instead of being left standing by a row this endpoint could not see.
/// </remarks>
sealed class NotificationMarkAllAsReadEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService, ITenantContext tenantContext) : EndpointWithoutRequest<NotificationMarkAllAsReadResponse>
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

        // Reading the active tenant here rather than leaning on the query filter is what lets the
        // platform-wide notifications back in: the filter alone would hide every row naming no tenant.
        // With no scope established this throws instead of marking rows the caller is not acting for.
        var activeTenantId = tenantContext.CurrentTenantId;

        // Platform scope names no tenant, and a database parameter carrying no value cannot be typed for
        // the comparison in the hand-written statement below, so the empty identifier stands in for it. It
        // matches no tenant's rows, which leaves the "names no tenant" half of the predicate as the only
        // one that can match - exactly the set platform scope should mark. No real tenant is ever
        // identified by the empty identifier, so the stand-in cannot collide with one.
        var tenantIdParameter = activeTenantId ?? Guid.Empty;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Tenant restriction is relaxed by name and narrowed straight back down by hand, the same way the
        // list and unread-count reads do it, so the rows this bulk update touches are the caller's own
        // notifications in the active tenant and nothing else. The soft-delete filter stays in force.
        await dbContext.Notifications
            .AcrossAllTenants()
            .Where(x => (x.TenantId == activeTenantId || x.TenantId == null)
                        && x.UserId == userId.Value
                        && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true), cancellationToken);

        // Read state for a notification addressed to an audience rather than to one user lives in a visit
        // row, so the second half is an insert. No query filter reaches a hand-written statement, which is
        // why its tenant predicate is spelled out: it admits the active tenant's own audience notifications
        // and the platform-wide ones, so an audience notification of another tenant is never visited here.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO notifications."NotificationVisits" ("Id", "UserId", "VisitedAt", "NotificationId")
            SELECT gen_random_uuid(), {{userId.Value}}, NOW(), notification."Id"
            FROM notifications."Notifications" AS notification
            WHERE notification."UserId" IS NULL AND notification."IsDeleted" = FALSE
              AND (notification."TenantId" = {{tenantIdParameter}} OR notification."TenantId" IS NULL)
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
