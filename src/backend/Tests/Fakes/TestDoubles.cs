namespace Backend.Tests.Fakes;

using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Registers the implementations the test host substitutes for production ones.
/// </summary>
/// <remarks>
/// These run through <c>ConfigureTestServices</c>, which is applied after the application's own
/// registrations, so each one replaces what the feature registered rather than competing with it.
/// </remarks>
public static class TestDoubles
{
    /// <summary>
    /// Substitutes the cheap password hasher and the mail service that sends nothing.
    /// </summary>
    public static IServiceCollection RegisterTestDoubles(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, TestPasswordHasher>();
        services.AddScoped<IEmailService, NoOpEmailService>();
        services.AddSessionStoreFaults();
        return services;
    }

    /// <summary>
    /// Decorates the session store the host registered (the shared in-memory one) with one that can be told
    /// to fail for particular sessions and accounts - the only way a test can make the store "unreachable"
    /// without touching any other test running beside it.
    /// </summary>
    private static IServiceCollection AddSessionStoreFaults(this IServiceCollection services)
    {
        var registered = services.Last(descriptor => descriptor.ServiceType == typeof(ISessionStore));
        services.Remove(registered);

        services.AddSingleton<SessionStoreFaults>();
        services.AddSingleton<ISessionStore>(provider => new FaultInjectingSessionStore(
            (ISessionStore)ActivatorUtilities.CreateInstance(provider, registered.ImplementationType!),
            provider.GetRequiredService<SessionStoreFaults>()));

        return services;
    }
}
