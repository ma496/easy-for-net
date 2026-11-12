using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Tests.Features.Notifications.Endpoints.Notifications;

namespace Backend.Tests.Features.Notifications.Core;

/// <summary>
/// Tests for <see cref="INotificationRetentionService"/>: the daily job that hard-deletes notifications past
/// their retention period and prunes the read visit rows a read cursor already covers.
/// </summary>
/// <remarks>
/// <para>
/// The job is global - it spans every user - so these tests only ever assert on rows they
/// created themselves, identified by fresh ids, and they never create rows the job could remove from under
/// another test: "old" rows are dated far past any retention period (<see cref="_oldAge"/>), which no other
/// test writes, and the visits and cursors belong to fresh user ids no other test knows. The class shares the
/// <c>Notifications</c> collection (through <see cref="NotificationsTestsBase"/>), so the job never runs while
/// another notification test is between writing a visit row and asserting on it.
/// </para>
/// <para>
/// Rows are written, then dated by a hand-written <c>UPDATE</c>, because the audit stamping sets
/// <c>CreatedAt</c> on save. The rows a test keeps are removed again when it ends, so the broadcasts it
/// arranged do not linger in other tests' lists.
/// </para>
/// </remarks>
public class NotificationRetentionServiceTests(App app) : NotificationsTestsBase(app)
{
    /// <summary>An age well past the default 90-day retention period.</summary>
    private static readonly TimeSpan _oldAge = TimeSpan.FromDays(400);

    /// <summary>An age well inside the retention period.</summary>
    private static readonly TimeSpan _recentAge = TimeSpan.FromDays(1);

    private readonly List<Guid> _arrangedNotificationIds = [];
    private readonly List<Guid> _arrangedUserIds = [];

    private INotificationRetentionService RetentionService => Service<INotificationRetentionService>();

    [Fact]
    public async Task Deletes_Old_Notifications_And_Keeps_Recent_Ones()
    {
        try
        {
            var recipient = NewUserId();
            var oldPersonal = await ArrangeAsync(recipient, _oldAge);
            var oldBroadcast = await ArrangeAsync(null, _oldAge);
            var oldDeleted = await ArrangeAsync(recipient, _oldAge, softDeleted: true);
            var recentPersonal = await ArrangeAsync(recipient, _recentAge);
            var recentBroadcast = await ArrangeAsync(null, _recentAge);

            await RetentionService.DeleteExpiredAsync(TestContext.Current.CancellationToken);

            var remaining = await ExistingNotificationIdsAsync([oldPersonal, oldBroadcast, oldDeleted, recentPersonal, recentBroadcast]);
            remaining.Should().BeEquivalentTo([recentPersonal, recentBroadcast],
                "a notification past its retention period is removed whoever it is addressed to, soft-deleted or not");
        }
        finally
        {
            await CleanUpAsync();
        }
    }

    [Fact]
    public async Task Prunes_Read_Visits_The_Users_Cursor_Covers()
    {
        try
        {
            var reader = NewUserId();
            var otherReader = NewUserId();
            await ArrangeCursorAsync(reader, TimeSpan.FromDays(10));

            var covered = await ArrangeAsync(null, TimeSpan.FromDays(20));
            var newer = await ArrangeAsync(null, TimeSpan.FromDays(5));

            var redundant = await ArrangeVisitAsync(covered, reader, isRead: true);
            var readAboveCursor = await ArrangeVisitAsync(newer, reader, isRead: true);
            var readWithoutCursor = await ArrangeVisitAsync(covered, otherReader, isRead: true);

            // An unread visit under a covering cursor: it overrides the cursor, so it stays.
            var unreadReader = NewUserId();
            await ArrangeCursorAsync(unreadReader, TimeSpan.FromDays(10));
            var unreadCovered = await ArrangeVisitAsync(covered, unreadReader, isRead: false);

            await RetentionService.DeleteExpiredAsync(TestContext.Current.CancellationToken);

            var remaining = await ExistingVisitIdsAsync([redundant, readAboveCursor, readWithoutCursor, unreadCovered]);

            remaining.Should().NotContain(redundant, "a read visit the user's cursor covers repeats what the cursor says");
            remaining.Should().Contain(unreadCovered, "an unread visit overrides the cursor, so it carries the only record that the notification reads as unread");
            remaining.Should().Contain(readAboveCursor, "a read visit on a notification newer than the cursor is the only thing marking it read");
            remaining.Should().Contain(readWithoutCursor, "another user's cursor covers nothing of this one's");

            (await ExistingNotificationIdsAsync([covered, newer])).Should().HaveCount(2, "pruning visits removes no notification");
        }
        finally
        {
            await CleanUpAsync();
        }
    }

    private Guid NewUserId()
    {
        var userId = Guid.NewGuid();
        _arrangedUserIds.Add(userId);
        return userId;
    }

    /// <summary>
    /// Writes a notification, then dates it <paramref name="age"/> ago.
    /// </summary>
    private async Task<Guid> ArrangeAsync(Guid? userId, TimeSpan age, bool softDeleted = false)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.Info,
            TitleKey = $"test.title.retention.{Guid.NewGuid()}",
            MessageKey = $"test.message.retention.{Guid.NewGuid()}",
        };

        DbContext.Notifications.Add(notification);
        await DbContext.SaveChangesAsync(cancellationToken);

        _arrangedNotificationIds.Add(notification.Id);

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

    /// <summary>Writes a read cursor set <paramref name="age"/> ago, by hand as mark-all-as-read does.</summary>
    private async Task ArrangeCursorAsync(Guid userId, TimeSpan age)
    {
        var readAllAt = DateTime.UtcNow - age;
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notifications."NotificationReadCursors" ("Id", "UserId", "ReadAllAt")
            VALUES (gen_random_uuid(), {userId}, {readAllAt})
            """, TestContext.Current.CancellationToken);
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
    /// the broadcasts it wrote reach no other test.
    /// </summary>
    private async Task CleanUpAsync()
    {
        var notificationIds = _arrangedNotificationIds.ToArray();
        var userIds = _arrangedUserIds.ToArray();
        var cancellationToken = CancellationToken.None;

        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications."Notifications" WHERE "Id" = ANY({notificationIds})
            """, cancellationToken);
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM notifications."NotificationReadCursors" WHERE "UserId" = ANY({userIds})
            """, cancellationToken);
    }
}
