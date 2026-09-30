namespace Backend.Tests.Features.Notifications.Core;

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Backend.Features.Notifications.Core.Push;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// One real client connection to the notification hub, over the test server's in-memory WebSocket, that
/// records every message it receives so a test can wait for one or show that one never came.
/// </summary>
/// <remarks>
/// It connects the way a non-browser bearer client does: WebSockets only, no negotiation, and the access
/// token in the <c>access_token</c> query string - the one place the API reads a token from a query.
/// The sentinels it sends itself (to learn that the connection has joined its groups, or that earlier
/// messages have been delivered) are kept out of <see cref="Notifications"/> and
/// <see cref="WaitForNotificationAsync"/>, so an assertion never sees one - its own or one sent to another
/// probe of the same account in the same scope, which shares its group.
/// </remarks>
public sealed class HubProbe : IAsyncDisposable
{
    /// <summary>How long a test waits for something that should arrive.</summary>
    private static readonly TimeSpan ArrivalTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How often <see cref="ConnectAsync"/> re-sends its readiness sentinel while it waits.</summary>
    private static readonly TimeSpan ReadinessResendInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The title key every sentinel carries, which is what keeps them out of the assertions.</summary>
    private const string SentinelTitleKey = "test.sentinel";

    private readonly ConcurrentQueue<NotificationReceivedMessage> _notifications = new();
    private readonly ConcurrentQueue<UnreadCountChangedMessage> _unreadCounts = new();
    private readonly TaskCompletionSource<Exception?> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HubProbe(HubConnection connection, Guid userId, Guid? tenantId)
    {
        Connection = connection;
        UserId = userId;
        TenantId = tenantId;

        connection.On<NotificationReceivedMessage>(NotificationHubMethods.NotificationReceived, _notifications.Enqueue);
        connection.On<UnreadCountChangedMessage>(NotificationHubMethods.UnreadCountChanged, _unreadCounts.Enqueue);
        connection.Closed += exception =>
        {
            _closed.TrySetResult(exception);
            return Task.CompletedTask;
        };
    }

    /// <summary>The client connection.</summary>
    public HubConnection Connection { get; }

    /// <summary>The account the connection authenticated as.</summary>
    public Guid UserId { get; }

    /// <summary>The tenant its session acts in, or <see langword="null"/> for platform scope.</summary>
    public Guid? TenantId { get; }

    /// <summary>The group the hub joined this connection to for its account in its scope.</summary>
    public string UserGroup => NotificationGroups.User(UserId, TenantId);

    /// <summary>Every notification message received so far.</summary>
    public IReadOnlyCollection<NotificationReceivedMessage> Notifications => [.. _notifications.Where(message => !IsSentinel(message))];

    /// <summary>Every unread-count message received so far.</summary>
    public IReadOnlyCollection<UnreadCountChangedMessage> UnreadCounts => [.. _unreadCounts];

    /// <summary>Completes with the close error (or <see langword="null"/>) when the server closes the connection.</summary>
    public Task<Exception?> Closed => _closed.Task;

    /// <summary>
    /// Builds a connection for an access token without starting it.
    /// </summary>
    /// <param name="server">The test server.</param>
    /// <param name="accessToken">The token to pass as <c>access_token</c>, or <see langword="null"/> for none.</param>
    /// <param name="userId">The account the token belongs to (<see cref="Guid.Empty"/> for none).</param>
    /// <param name="tenantId">The tenant its session acts in.</param>
    /// <param name="origin">
    /// The <c>Origin</c> header to send with the upgrade, as a browser would, or <see langword="null"/> to send
    /// none, as a non-browser client does.
    /// </param>
    /// <returns>The unstarted probe.</returns>
    public static HubProbe Create(TestServer server, string? accessToken, Guid userId, Guid? tenantId, string? origin = null)
    {
        var hubUri = new Uri(server.BaseAddress, NotificationHub.Path);
        var socketUri = accessToken is null
            ? hubUri
            : new Uri($"{hubUri}?access_token={Uri.EscapeDataString(accessToken)}");
        var webSocketClient = server.CreateWebSocketClient();
        if (origin is not null)
        {
            webSocketClient.ConfigureRequest = request => request.Headers.Origin = origin;
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (_, cancellationToken) => await webSocketClient.ConnectAsync(socketUri, cancellationToken);
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        return new HubProbe(connection, userId, tenantId);
    }

    /// <summary>
    /// Connects with an access token and returns the probe once the connection has joined every group its
    /// session names.
    /// </summary>
    /// <remarks>
    /// SignalR answers the handshake before the hub's <c>OnConnectedAsync</c> runs, so <c>StartAsync</c>
    /// completing says nothing about the groups; a push sent straight after it can miss the connection. The
    /// hub joins the account's own <c>u:</c> group last, so this sends a sentinel to that group - again every
    /// <see cref="ReadinessResendInterval"/>, since one sent before the join reaches nobody - until one
    /// arrives, which proves every earlier join has finished. Only for a connection expected to be admitted:
    /// a refused one is built with <see cref="Create"/> and started by the test.
    /// </remarks>
    public static async Task<HubProbe> ConnectAsync(TestServer server, string accessToken, Guid userId, Guid? tenantId)
    {
        var probe = Create(server, accessToken, userId, tenantId);
        try
        {
            await probe.Connection.StartAsync(TestContext.Current.CancellationToken);
            await probe.WaitUntilJoinedAsync(server.Services.GetRequiredService<IHubContext<NotificationHub>>());
        }
        catch
        {
            await probe.DisposeAsync();
            throw;
        }

        return probe;
    }

    /// <summary>
    /// Waits until a notification matching the predicate has arrived, failing the test if none does in time.
    /// </summary>
    /// <param name="predicate">What the notification must satisfy.</param>
    /// <returns>The notification.</returns>
    public async Task<NotificationReceivedMessage> WaitForNotificationAsync(Func<NotificationReceivedMessage, bool> predicate)
        => await WaitForAsync(_notifications, message => !IsSentinel(message) && predicate(message), "notificationReceived");

    /// <summary>
    /// Waits until an unread count has arrived, failing the test if none does in time.
    /// </summary>
    /// <returns>The first unread-count message.</returns>
    public async Task<UnreadCountChangedMessage> WaitForUnreadCountAsync()
        => await WaitForAsync(_unreadCounts, _ => true, "unreadCountChanged");

    /// <summary>
    /// Proves that everything sent to this connection before now has been delivered: sends a sentinel
    /// straight to the connection's own group and waits for it. Messages to one connection arrive in the
    /// order they were sent, so once the sentinel is in, anything published to this connection earlier is
    /// in too - which is what lets a test assert a message never came without sleeping.
    /// </summary>
    /// <param name="sender">The host's hub sender.</param>
    public async Task DrainAsync(INotificationHubSender sender)
    {
        var sentinelId = Guid.NewGuid();
        await sender.SendAsync(UserGroup, NotificationHubMethods.NotificationReceived, Sentinel(sentinelId), TestContext.Current.CancellationToken);

        await WaitForAsync(_notifications, message => message.Id == sentinelId, "sentinel");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();

    /// <summary>
    /// Sends a sentinel to the connection's own group until one arrives, failing if the server closes the
    /// connection first or none arrives in time.
    /// </summary>
    private async Task WaitUntilJoinedAsync(IHubContext<NotificationHub> hubContext)
    {
        var sentinelId = Guid.NewGuid();
        var deadline = DateTime.UtcNow + ArrivalTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await hubContext.Clients.Group(UserGroup).SendAsync(
                NotificationHubMethods.NotificationReceived, Sentinel(sentinelId), TestContext.Current.CancellationToken);

            var resendAt = DateTime.UtcNow + ReadinessResendInterval;
            while (DateTime.UtcNow < resendAt)
            {
                if (_notifications.Any(message => message.Id == sentinelId))
                {
                    return;
                }

                if (Closed.IsCompleted)
                {
                    throw new InvalidOperationException("The hub closed the connection before it joined its groups.", await Closed);
                }

                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        throw new TimeoutException($"The connection did not join its groups within {ArrivalTimeout.TotalSeconds} seconds.");
    }

    private static NotificationReceivedMessage Sentinel(Guid id) => new()
    {
        Id = id,
        TitleKey = SentinelTitleKey,
        MessageKey = SentinelTitleKey
    };

    private static bool IsSentinel(NotificationReceivedMessage message) => message.TitleKey == SentinelTitleKey;

    private static async Task<T> WaitForAsync<T>(ConcurrentQueue<T> queue, Func<T, bool> predicate, string what)
    {
        var deadline = DateTime.UtcNow + ArrivalTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var match = queue.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No matching {what} message arrived within {ArrivalTimeout.TotalSeconds} seconds.");
    }
}
