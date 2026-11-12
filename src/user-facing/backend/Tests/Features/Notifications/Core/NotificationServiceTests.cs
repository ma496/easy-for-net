using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Tests.Features.Notifications.Endpoints.Notifications;

namespace Backend.Tests.Features.Notifications.Core;

/// <summary>
/// Tests for <see cref="INotificationService"/> covering its two addressing modes - a single account and
/// every user - read back both as the row it persisted and through the list a recipient is answered from.
/// </summary>
public class NotificationServiceTests(App app) : NotificationsTestsBase(app)
{
    private INotificationService NotificationService => Service<INotificationService>();

    private static string NewTitleKey() => $"test.title.service.{Guid.NewGuid()}";

    [Fact]
    public async Task Addresses_A_Single_Account()
    {
        var recipient = await CreateUserAsync();
        var bystander = await CreateUserAsync();
        var titleKey = NewTitleKey();

        await NotificationService.NewUserNotificationAsync(
            recipient.Id, NotificationType.Warning, titleKey, $"{titleKey}.message", "group", "{}", TestContext.Current.CancellationToken);

        var stored = await NotificationByTitleAsync(titleKey);
        stored.UserId.Should().Be(recipient.Id);
        stored.Type.Should().Be(NotificationType.Warning);
        stored.Group.Should().Be("group");

        (await SearchIdsAsync(await ClientForAsync(recipient.Username), titleKey)).Should().Equal([stored.Id]);
        (await SearchIdsAsync(await ClientForAsync(bystander.Username), titleKey)).Should().BeEmpty("it is addressed to one account");
    }

    [Fact]
    public async Task Addresses_Every_User()
    {
        var first = await CreateUserAsync();
        var second = await CreateUserAsync();
        var titleKey = NewTitleKey();

        await NotificationService.NewAllUsersNotificationAsync(
            NotificationType.Info, titleKey, $"{titleKey}.message", cancellationToken: TestContext.Current.CancellationToken);

        var stored = await NotificationByTitleAsync(titleKey);
        stored.UserId.Should().BeNull("a broadcast names no recipient");

        (await SearchIdsAsync(await ClientForAsync(first.Username), titleKey)).Should().Equal([stored.Id]);
        (await SearchIdsAsync(await ClientForAsync(second.Username), titleKey)).Should().Equal([stored.Id]);
    }

    [Fact]
    public async Task Counts_Unread_For_The_Recipient()
    {
        var recipient = await CreateUserAsync();
        var before = await NotificationService.GetUnreadCountAsync(recipient.Id, TestContext.Current.CancellationToken);

        await NotificationService.NewUserNotificationAsync(
            recipient.Id, NotificationType.Info, NewTitleKey(), "message", cancellationToken: TestContext.Current.CancellationToken);

        (await NotificationService.GetUnreadCountAsync(recipient.Id, TestContext.Current.CancellationToken)).Should().Be(before + 1);
    }

    private async Task<Notification> NotificationByTitleAsync(string titleKey)
        => await DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(notification => notification.TitleKey == titleKey, TestContext.Current.CancellationToken);
}
