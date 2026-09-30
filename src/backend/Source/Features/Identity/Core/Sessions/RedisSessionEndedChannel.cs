namespace Backend.Features.Identity.Core.Sessions;

using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

/// <summary>
/// The Redis pub/sub channel ended sessions are announced on, so every API instance - not only the one that
/// ended a session - lets go of what it holds for it.
/// </summary>
/// <remarks>
/// The channel is <c>{Redis:InstanceName}sessions:ended</c>, prefixed the way every session key is, so two
/// deployments sharing one Redis never hear each other. Each message names the process that sent it, which
/// has already told its own handlers, so a process ignores its own messages.
/// </remarks>
public static class RedisSessionEndedChannel
{
    /// <summary>The identity of this process on the channel.</summary>
    public static readonly string Origin = Guid.NewGuid().ToString("N");

    /// <summary>The channel for a deployment's key prefix.</summary>
    /// <param name="instanceName">The <c>Redis:InstanceName</c> prefix.</param>
    /// <returns>The literal channel.</returns>
    public static RedisChannel For(string? instanceName) => RedisChannel.Literal($"{instanceName}sessions:ended");

    /// <summary>Serializes the message this process sends for a set of ended sessions.</summary>
    /// <param name="sessionIds">The ended sessions.</param>
    /// <returns>The payload.</returns>
    public static string Serialize(IReadOnlyCollection<string> sessionIds)
        => JsonSerializer.Serialize(new SessionEndedMessage(Origin, [.. sessionIds]));

    /// <summary>
    /// Reads a message, answering <see langword="null"/> for one that cannot be read or that this process
    /// sent itself.
    /// </summary>
    /// <param name="payload">The payload received.</param>
    /// <returns>The sessions another process ended, or <see langword="null"/>.</returns>
    public static IReadOnlyCollection<string>? ReadFromOthers(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize<SessionEndedMessage>(payload);
            if (message?.SessionIds is null || message.Origin == Origin)
            {
                return null;
            }

            return [.. message.SessionIds.Where(sessionId => !string.IsNullOrEmpty(sessionId))];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record SessionEndedMessage(
        [property: JsonPropertyName("o")] string Origin,
        [property: JsonPropertyName("s")] string[] SessionIds);
}

/// <summary>
/// The publisher deployments use: tells this process's handlers at once, then announces the sessions on
/// <see cref="RedisSessionEndedChannel"/> for every other instance's <see cref="RedisSessionEndedSubscriber"/>.
/// Telling the local handlers first means a Redis that cannot be reached still leaves this instance's own
/// connections closed; the failure to reach the others is logged.
/// </summary>
/// <param name="dispatcher">This process's handlers.</param>
/// <param name="multiplexer">The Redis connection the session store uses.</param>
/// <param name="setting">The Redis settings, for the channel prefix.</param>
/// <param name="logger">Logs a publish that failed.</param>
[NoDirectUse]
public sealed class RedisSessionEndedPublisher(
    LocalSessionEndedDispatcher dispatcher,
    IConnectionMultiplexer multiplexer,
    IOptions<RedisSetting> setting,
    ILogger<RedisSessionEndedPublisher> logger) : ISessionEndedPublisher
{
    private readonly RedisChannel _channel = RedisSessionEndedChannel.For(setting.Value.InstanceName);

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyCollection<string> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return;
        }

        await dispatcher.DispatchAsync(sessionIds);

        try
        {
            await multiplexer.GetSubscriber().PublishAsync(_channel, RedisSessionEndedChannel.Serialize(sessionIds));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Announcing {SessionCount} ended session(s) to the other API instances failed; their connections close when their credentials expire.",
                sessionIds.Count);
        }
    }
}

/// <summary>
/// Subscribes this instance to <see cref="RedisSessionEndedChannel"/> and tells its handlers about the
/// sessions other instances ended.
/// </summary>
/// <remarks>
/// The host starts while Redis is down (the session store answers 503 meanwhile), so this never blocks
/// startup and never throws out of it: the multiplexer is resolved and the subscription made in the
/// background, retried with a growing delay until it succeeds. Once made, a subscription is restored by
/// StackExchange.Redis itself whenever the connection comes back. A subscription that failed is withdrawn
/// before the next attempt, so a retry never leaves two handlers delivering every message twice.
/// </remarks>
/// <param name="services">Where the multiplexer is resolved from, off the startup path.</param>
/// <param name="dispatcher">This process's handlers.</param>
/// <param name="setting">The Redis settings, for the channel prefix.</param>
/// <param name="logger">Logs subscription failures and unreadable messages.</param>
[NoDirectUse]
public sealed class RedisSessionEndedSubscriber(
    IServiceProvider services,
    LocalSessionEndedDispatcher dispatcher,
    IOptions<RedisSetting> setting,
    ILogger<RedisSessionEndedSubscriber> logger) : BackgroundService
{
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LongestRetryDelay = TimeSpan.FromSeconds(30);

    private readonly RedisChannel _channel = RedisSessionEndedChannel.For(setting.Value.InstanceName);
    private ISubscriber? _subscriber;
    private Action<RedisChannel, RedisValue>? _handler;

    /// <summary>The one delegate subscribed, kept so exactly it is withdrawn.</summary>
    private Action<RedisChannel, RedisValue> Handler => _handler ??= OnMessage;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Off the startup path before anything else: connecting the multiplexer to a Redis that is down
        // waits out its connect timeout.
        await Task.Yield();

        var delay = FirstRetryDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            ISubscriber? subscriber = null;
            try
            {
                subscriber = services.GetRequiredService<IConnectionMultiplexer>().GetSubscriber();
                await subscriber.SubscribeAsync(_channel, Handler);
                _subscriber = subscriber;
                logger.LogInformation("Subscribed to ended-session announcements on {Channel}.", _channel.ToString());
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception,
                    "Subscribing to ended-session announcements failed; retrying in {Delay}. Connections on this instance whose session another instance ends stay open until then.",
                    delay);
                await WithdrawAsync(subscriber);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, LongestRetryDelay.Ticks));
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await WithdrawAsync(_subscriber);
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        var sessionIds = RedisSessionEndedChannel.ReadFromOthers(value.IsNull ? null : value.ToString());
        if (sessionIds is null || sessionIds.Count == 0)
        {
            return;
        }

        // Handled off the thread delivering messages; the dispatcher never throws.
        _ = Task.Run(() => dispatcher.DispatchAsync(sessionIds));
    }

    private async Task WithdrawAsync(ISubscriber? subscriber)
    {
        if (subscriber is null)
        {
            return;
        }

        try
        {
            await subscriber.UnsubscribeAsync(_channel, Handler);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Withdrawing the ended-session subscription failed.");
        }
    }
}
