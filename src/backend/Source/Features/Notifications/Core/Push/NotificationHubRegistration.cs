namespace Backend.Features.Notifications.Core.Push;

using System.Text.Json.Serialization;
using Backend.Features.Identity.Core.Sessions;
using Backend.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Net.Http.Headers;
using StackExchange.Redis;

/// <summary>
/// Registers and maps the notification hub. Called from <c>Program.cs</c> rather than from the slice's
/// <c>AddServices</c>, because whether the Redis backplane is used depends on the environment, which a
/// feature module does not receive.
/// </summary>
[BypassNoDirectUse]
public static class NotificationHubRegistration
{
    /// <summary>
    /// The query-string parameter a bearer client passes its access token in. Browsers cannot set a header
    /// on a WebSocket, but they send the auth cookie, so this is for non-browser clients.
    /// </summary>
    public const string AccessTokenQueryParameter = "access_token";

    /// <summary>
    /// The largest message a client may send. The hub declares no client-callable method, so all a client
    /// ever sends is the handshake and keep-alive pings; anything larger is not a legitimate message.
    /// </summary>
    private const long MaximumReceiveMessageSize = 4 * 1024;

    /// <summary>
    /// Adds SignalR with the hub's options, the JSON protocol serializing enums by name (as the list endpoint
    /// does), the Redis backplane when asked for, the WebSocket origin check, and the reading of
    /// <see cref="AccessTokenQueryParameter"/> on the hub's path.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="configuration">The application's configuration.</param>
    /// <param name="useBackplane">
    /// <see langword="true"/> everywhere but the <c>Testing</c> environment: every API instance subscribes to
    /// <c>ConnectionStrings:Redis</c>, so a publish from any process - a request, or a Hangfire job - reaches
    /// connections held by every other. The test host runs one process and needs PostgreSQL only.
    /// </param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddNotificationHub(this IServiceCollection services, IConfiguration configuration, bool useBackplane)
    {
        services.AddSingleton<NotificationConnectionRegistry>();

        // Identity tells every instance when a session ends; this closes the connections it authenticated.
        services.AddSingleton<ISessionEndedHandler, NotificationSessionEndedHandler>();

        // Keep-alive and client timeout stay at SignalR's defaults (15 s / 30 s): nothing here needs them
        // tighter, and the web client is written against the defaults.
        var signalR = services
            .AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = MaximumReceiveMessageSize;
                options.EnableDetailedErrors = false;
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        if (useBackplane)
        {
            var connectionString = configuration.GetConnectionString("Redis")
                                   ?? throw new InvalidOperationException("ConnectionStrings:Redis is required for the notification hub's backplane.");
            var instanceName = configuration["Redis:InstanceName"] ?? string.Empty;

            signalR.AddStackExchangeRedis(connectionString, options =>
            {
                // The channel prefix keeps two deployments sharing one Redis from hearing each other's
                // pushes, the same way Redis:InstanceName keeps their session keys apart. Not aborting on a
                // failed connect lets the API start while Redis is down, as the session store does.
                options.Configuration.ChannelPrefix = RedisChannel.Literal(instanceName);
                options.Configuration.AbortOnConnectFail = false;
            });
        }

        // A WebSocket upgrade is not a CORS request, so the CORS policy does not stop a page on another origin
        // - a sibling host on the same site, whose requests carry the auth cookie - from opening the hub as
        // its visitor. The WebSocket middleware SignalR runs for the hub refuses, with 403, an upgrade whose
        // Origin is not one of the web app's domains: the same list the CORS policy allows. An upgrade with
        // no Origin at all (a non-browser bearer client) is not a browser's, carries no ambient cookie, and
        // is let through.
        services.AddOptions<Microsoft.AspNetCore.Builder.WebSocketOptions>()
            .Configure<IOptions<WebSetting>>((webSocketOptions, webSetting) =>
            {
                foreach (var domain in webSetting.Value.AllowedDomains())
                {
                    webSocketOptions.AllowedOrigins.Add(domain);
                }
            });

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var previous = options.Events.OnMessageReceived;
            options.Events.OnMessageReceived = async context =>
            {
                await previous(context);

                // Only on the hub's path, and only when no header carries a token: everywhere else a query
                // string token is ignored, so a link or a log line carrying one cannot sign anybody in.
                if (context.Token is null && CarriesQueryAccessToken(context.Request))
                {
                    context.Token = context.Request.Query[AccessTokenQueryParameter].ToString();
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Maps the hub: WebSockets only (long polling and server-sent events are refused), closed when the
    /// credential it authenticated with expires, and behind the default authorization policy, so it is
    /// refused with 401 exactly when an HTTP request with the same credential would be.
    /// </summary>
    /// <param name="app">The application to map the hub on.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapNotificationHub(this WebApplication app)
    {
        app.MapHub<NotificationHub>(NotificationHub.Path, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.CloseOnAuthenticationExpiration = true;
            })
            .RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Whether a request is one whose bearer token travels in the query string: a request to the hub's path
    /// carrying <see cref="AccessTokenQueryParameter"/> and no <c>Authorization</c> header. The
    /// authentication policy scheme asks this to choose JWT bearer for it, and the bearer handler to read
    /// the token from the query; on any other path the parameter means nothing.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> for a hub request authenticating through the query string.</returns>
    public static bool CarriesQueryAccessToken(HttpRequest request)
        => request.Path.StartsWithSegments(NotificationHub.Path, StringComparison.OrdinalIgnoreCase) &&
           !request.Headers.ContainsKey(HeaderNames.Authorization) &&
           !string.IsNullOrEmpty(request.Query[AccessTokenQueryParameter]);
}
