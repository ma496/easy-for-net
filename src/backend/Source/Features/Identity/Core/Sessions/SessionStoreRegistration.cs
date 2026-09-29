namespace Backend.Features.Identity.Core.Sessions;

using StackExchange.Redis;

/// <summary>
/// Chooses the <see cref="ISessionStore"/> a host runs on: Redis everywhere except the test host, which
/// shares one in-memory store across the whole run so the suite needs nothing but PostgreSQL.
/// </summary>
[BypassNoDirectUse]
public static class SessionStoreRegistration
{
    /// <summary>
    /// Registers the session store as a singleton, and the Redis settings it reads.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="configuration">The application's configuration.</param>
    /// <param name="useInMemoryStore">
    /// <see langword="true"/> under the <c>Testing</c> environment only, where sessions live in one
    /// process-wide dictionary; every other environment uses Redis.
    /// </param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSessionStore(this IServiceCollection services, IConfiguration configuration, bool useInMemoryStore)
    {
        services.Configure<RedisSetting>(configuration.GetSection("Redis"));

        if (useInMemoryStore)
        {
            services.AddSingleton<ISessionStore, InMemorySessionStore>();
            return services;
        }

        var connectionString = configuration.GetConnectionString("Redis")
                               ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

        // AbortOnConnectFail = false is what lets the host start while Redis is down: the multiplexer
        // keeps trying in the background, and every request that needs a session meanwhile fails closed
        // with SessionStoreUnavailableException rather than the process refusing to boot. FailFast makes
        // those requests fail at once instead of queuing until they time out.
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.BacklogPolicy = BacklogPolicy.FailFast;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<ISessionStore, RedisSessionStore>();

        return services;
    }
}
