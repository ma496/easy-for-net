namespace Backend.Tests.Fakes;

using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Settings.Core;

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
    /// Substitutes the cheap password hasher and the mail service that sends nothing, adds the probe
    /// setting the settings suite writes freely, and lets a test overlay platform settings per tenant.
    /// </summary>
    public static IServiceCollection RegisterTestDoubles(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, TestPasswordHasher>();
        services.AddScoped<IEmailService, NoOpEmailService>();
        services.AddSessionStoreFaults();
        services.AddSingleton<ISettingDefinitionProvider, ProbeSettingsProvider>();
        services.AddPlatformSettingOverlays();
        return services;
    }

    /// <summary>
    /// Decorates the setting store the host registered with one that reports, for the tenants a test
    /// names, the platform overrides that test chose - see <see cref="PlatformSettingOverlays"/>.
    /// </summary>
    private static IServiceCollection AddPlatformSettingOverlays(this IServiceCollection services)
    {
        var registered = services.Last(descriptor => descriptor.ServiceType == typeof(ISettingValueStore));
        services.Remove(registered);

        services.AddSingleton<PlatformSettingOverlays>();
        services.AddScoped<ISettingValueStore>(provider => new OverlayingSettingValueStore(
            (ISettingValueStore)ActivatorUtilities.CreateInstance(provider, registered.ImplementationType!),
            provider.GetRequiredService<PlatformSettingOverlays>()));

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
