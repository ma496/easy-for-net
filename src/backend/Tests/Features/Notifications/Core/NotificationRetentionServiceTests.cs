namespace Backend.Tests.Features.Notifications.Core;

using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Tests.Features.Notifications.Endpoints.Notifications;

/// <summary>
/// Tests for <see cref="INotificationRetentionService"/>: the daily job that hard-deletes notifications past
/// their retention period and prunes the read visit rows a read cursor already covers.
/// </summary>
/// <remarks>
/// <para>
/// The job is global - it spans every tenant and every user - so these tests only ever assert on rows they
/// created themselves, identified by fresh ids, and they never create rows the job could remove from under
/// another test: "old" rows are dated far past any retention period (<see cref="OldAge"/>), which no other
/// test writes, and the visits and cursors belong to fresh user ids no other test knows. The class shares the
/// <c>Notifications</c> collection (through <see cref="NotificationsTestsBase"/>), so the job never runs while
/// another notification test is between writing a visit row and asserting on it.
/// </para>
/// <para>
/// Rows are attributed through the scope they belong to, then dated by a hand-written <c>UPDATE</c>, because
/// the audit stamping sets <c>CreatedAt</c> on save. The rows a test keeps are removed again when it ends, so
/// the audience rows it arranged in the seeded tenants do not linger in other tests' lists.
/// </para>
/// </remarks>
public class NotificationRetentionServiceTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>An age well past the default 90-day retention period.</summary>
    private static readonly TimeSpan OldAge = TimeSpan.FromDays(400);

    /// <summary>An age well inside the retention period.</summary>
    private static readonly TimeSpan RecentAge = TimeSpan.FromDays(1);

    private readonly List<Guid> arrangedNotificationIds = [];
    private readonly List<Guid> arrangedUserIds = [];

    private INotificationRetentionService RetentionService => Service<INotificationRetentionService>();

    /// <summary>
    /// Verifies that notifications older than the retention period are hard-deleted - personal and audience,
    /// in two tenants and in platform scope, soft-deleted or not - along with their visits, and that the
    /// recent ones beside them are kept.
    /// </summary>
    [Fact]
    public async Task Deletes_Old_Notifications_In_Every_Scope_And_Keeps_Recent_Ones()
    {
        try
        {
            var firstTenant = TestTenants.BootstrapTenantId;
            var secondTenant = TestTenants.SecondTenantId;
            var tenantMember = NewUserId();
            var platformUser = NewUserId();
            var reader = NewUserId();

            var old = new List<Guid>
            {
                await ArrangeAsync(firstTenant, tenantMember, OldAge),
                await ArrangeAsync(secondTenant, tenantMember, OldAge),
                await ArrangeAsync(firstTenant, null, OldAge),
                await ArrangeAsync(secondTenant, null, OldAge),
                await ArrangeAsync(null, null, OldAge),
                await ArrangeAsync(null, platformUser, OldAge),
            };
            var oldSoftDeleted = await ArrangeAsync(firstTenant, tenantMember, OldAge, softDeleted: true);

            var recent = new List<Guid>
            {
                await ArrangeAsync(firstTenant, tenantMember, RecentAge),
                await ArrangeAsync(secondTenant, tenantMember, RecentAge),
                await ArrangeAsync(firstTenant, null, RecentAge),
                await ArrangeAsync(secondTenant, null, RecentAge),
                await ArrangeAsync(null, null, RecentAge),
                await ArrangeAsync(null, platformUser, RecentAge),
            };
            var recentSoftDeleted = await ArrangeAsync(firstTenant, tenantMember, RecentAge, softDeleted: true);

            // Visits on an old tenant-wide and an old platform-wide row, unread so that no cursor rule could
            // be what removes them: only the cascade from the deleted notification can.
            var oldVisits = new[]
            {
                await ArrangeVisitAsync(old[2], reader, isRead: false),
                await ArrangeVisitAsync(old[4], reader, isRead: false),
            };
            var recentVisit = await ArrangeVisitAsync(recent[2], reader, isRead: false);

            await RetentionService.DeleteExpiredAsync(TestContext.Current.CancellationToken);

            var remaining = await ExistingNotificationIdsAsync([.. old, oldSoftDeleted, .. recent, recentSoftDeleted]);
            remaining.Should().BeEquivalentTo([.. recent, recentSoftDeleted],
                "every notification past the retention period is hard-deleted whatever its scope, audience or soft-delete state, and every recent one is kept");

            var remainingVisits = await ExistingVisitIdsAsync([.. oldVisits, recentVisit]);
            remainingVisits.Should().BeEquivalentTo([recentVisit],
                "the visits of a deleted notification go with it through the cascading foreign key");
        }
        finally
        {
            await CleanUpAsync();
        }
    }

    /// <summary>
    /// Verifies that a read visit the visiting user's cursor for the notification's audience already covers is
    /// deleted, and that every visit still carrying information is kept: an unread one below the cursor, a read
    /// one above it, and a read one whose only cursor is for a different audience.
    /// </summary>
    [Fact]
    public async Task Prunes_Read_Visits_A_Matching_Cursor_Covers()
    {
        try
        {
            var firstTenant = TestTenants.BootstrapTenantId;
            var secondTenant = TestTenants.SecondTenantId;
            var tenantReader = NewUserId();
            var platformReader = NewUserId();

            // tenantReader has a cursor for the first tenant only; platformReader a platform-wide one only.
            await ArrangeCursorAsync(tenantReader, firstTenant, TimeSpan.FromDays(10));
            await ArrangeCursorAsync(platformReader, null, TimeSpan.FromDays(10));

            var coveredTenantWide = await ArrangeAsync(firstTenant, null, TimeSpan.FromDays(20));
            var newerTenantWide = await ArrangeAsync(firstTenant, null, TimeSpan.FromDays(5));
            var otherTenantWide = await ArrangeAsync(secondTenant, null, TimeSpan.FromDays(20));
            var coveredPlatformWide = await ArrangeAsync(null, null, TimeSpan.FromDays(20));

            var redundantTenantVisit = await ArrangeVisitAsync(coveredTenantWide, tenantReader, isRead: true);
            var redundantPlatformVisit = await ArrangeVisitAsync(coveredPlatformWide, platformReader, isRead: true);

            var readAboveCursor = await ArrangeVisitAsync(newerTenantWide, tenantReader, isRead: true);
            var readOtherTenant = await ArrangeVisitAsync(otherTenantWide, tenantReader, isRead: true);
            var readPlatformWithTenantCursorOnly = await ArrangeVisitAsync(coveredPlatformWide, tenantReader, isRead: true);
            var readTenantWideWithPlatformCursorOnly = await ArrangeVisitAsync(coveredTenantWide, platformReader, isRead: true);

            // An unread visit under a covering cursor of its own audience: it overrides the cursor, so it stays.
            var unreadReader = NewUserId();
            await ArrangeCursorAsync(unreadReader, firstTenant, TimeSpan.FromDays(10));
            var unreadCovered = await ArrangeVisitAsync(coveredTenantWide, unreadReader, isRead: false);

            await RetentionService.DeleteExpiredAsync(TestContext.Current.CancellationToken);

            var arrangedVisits = new[]
            {
                redundantTenantVisit, redundantPlatformVisit, readAboveCursor, readOtherTenant,
                readPlatformWithTenantCursorOnly, readTenantWideWithPlatformCursorOnly, unreadCovered,
            };
            var remaining = await ExistingVisitIdsAsync(arrangedVisits);

            remaining.Should().NotContain(redundantTenantVisit,
                "a read visit on a tenant-wide notification the user's cursor for that tenant covers repeats what the cursor says");
            remaining.Should().NotContain(redundantPlatformVisit,
                "a read visit on a platform-wide notification the user's platform-wide cursor covers repeats what the cursor says");
            remaining.Should().Contain(unreadCovered,
                "an unread visit overrides the cursor, so it carries the only record that the notification reads as unread");
            remaining.Should().Contain(readAboveCursor,
                "a read visit on a notification newer than the cursor is the only thing marking it read");
            remaining.Should().Contain(readOtherTenant,
                "a cursor for one tenant covers nothing in another");
            remaining.Should().Contain(readPlatformWithTenantCursorOnly,
                "a tenant cursor does not cover a platform-wide notification");
            remaining.Should().Contain(readTenantWideWithPlatformCursorOnly,
                "a platform-wide cursor covers no tenant-wide notification");

            var notifications = await ExistingNotificationIdsAsync([coveredTenantWide, newerTenantWide, otherTenantWide, coveredPlatformWide]);
            notifications.Should().HaveCount(4, "pruning visits removes no notification");
        }
        finally
        {
            await CleanUpAsync();
        }
    }

    private Guid NewUserId()
    {
        var userId = Guid.NewGuid();
        arrangedUserIds.Add(userId);
        return userId;
    }

    /// <summary>
    /// Writes a notification in the scope that attributes it, then dates it <paramref name="age"/> ago.
    /// </summary>
    private async Task<Guid> ArrangeAsync(Guid? tenantId, Guid? userId, TimeSpan age, bool softDeleted = false)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var notification = new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = NotificationType.Info,
            TitleKey = $"test.title.retention.{Guid.NewGuid()}",
            MessageKey = $"test.message.retention.{Guid.NewGuid()}",
        };

        using (tenantId is { } activeTenantId ? TenantContext.BeginTenant(activeTenantId) : TenantContext.BeginPlatformScope())
        {
            DbContext.Notifications.Add(notification);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        arrangedNotificationIds.Add(notification.Id);

        var createdAt = DateTime.UtcNow - age;
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE notifications."Notifications"
            SET "CreatedAt" = {createdAt}, "IsDeleted" = {softDeleted}
            WHERE "Id" = {notification.Id}
            """, cancellationToken);

        return notification.Id;
    }

    private async Task<Guid> ArrangeVisitAsync(Guid notificationId, Guid userId, bool isRead)
    {
        var visit = new NotificationVisit
        {
            NotificationId = notificationId,
            UserId = userId,
            VisitedAt = DateTime.UtcNow,
            IsRead = isRead,
        };
        DbContext.NotificationVisits.Add(visit);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return visit.Id;
    }

    /// <summary>
    /// Writes a read cursor set <paramref name="age"/> ago, by hand as mark-all-as-read does, because a cursor
    /// naming no tenant cannot be saved through the change tracker while a tenant is active.
    /// </summary>
    private async Task ArrangeCursorAsync(Guid userId, Guid? tenantId, TimeSpan age)
    {
        var readAllAt = DateTime.UtcNow - age;
        var cancellationToken = TestContext.Current.CancellationToken;

        if (tenantId is { } id)
        {
            await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications."NotificationReadCursors" ("Id", "UserId", "TenantId", "ReadAllAt")
                VALUES (gen_random_uuid(), {userId}, {id}, {readAllAt})
                """, cancellationToken);
        }
        else
        {
            await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications."NotificationReadCursors" ("Id", "UserId", "TenantId", "ReadAllAt")
                VALUES (gen_random_uuid(), {userId}, NULL, {readAllAt})
                """, cancellationToken);
        }
    }

    private async Task<List<Guid>> ExistingNotificationIdsAsync(Guid[] ids)
        => await DbContext.Notifications
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(notification => ids.Contains(notification.Id))
            .Select(notification => notification.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

    private async Task<List<Guid>> ExistingVisitIdsAsync(Guid[] ids)
        => await DbContext.NotificationVisits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(visit => ids.Contains(visit.Id))
            .Select(visit => visit.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Removes what the test arranged and the job kept - its notifications (visits cascade) and its cursors - so
    /// the audience rows it wrote into the seeded tenants and platform scope reach no other test.
    /// </summary>
    private async Task CleanUpAsync()
    {
        var notificationIds = arrangedNotificationIds.ToArray();
        var userIds = arrangedUserIds.ToArray();
        var cancellationToken = CancellationToken.None;

        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications."Notifications" WHERE "Id" = ANY({notificationIds})
            """, cancellationToken);
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications."NotificationReadCursors" WHERE "UserId" = ANY({userIds})
            """, cancellationToken);
    }
}
