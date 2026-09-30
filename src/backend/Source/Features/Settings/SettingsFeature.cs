namespace Backend.Features.Settings;

using Backend.Features.Settings.Core;

/// <summary>
/// Feature module that registers the settings services with the DI container.
/// </summary>
/// <remarks>
/// The definition providers are discovered by reflection across the whole assembly, because every
/// slice declares the settings it owns. The catalogue composed from them is a singleton - it is code
/// and cannot change while the process runs - and is composed while the host starts, so a duplicate
/// name, a class registered twice or an invalid default stops startup. Resolution and the store are
/// scoped, because they read the scoped <see cref="AppDbContext"/> and <c>ITenantContext</c>, and the
/// per-request cache of stored values lives in them. Secret properties are encrypted with ASP.NET Data
/// Protection, registered here too (idempotently - the host's cookie authentication registers it as
/// well); a deployment running more than one instance must share the key ring between them, as it
/// already must for the auth cookie.
/// </remarks>
[BypassNoDirectUse]
public class SettingsFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        var settingProviders = typeof(SettingsFeature).Assembly.GetTypes()
            .Where(t => typeof(ISettingDefinitionProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        foreach (var provider in settingProviders)
        {
            services.AddSingleton(typeof(ISettingDefinitionProvider), provider);
        }

        services.AddSingleton<ISettingDefinitionCatalogue, SettingDefinitionCatalogue>();
        services.AddHostedService<SettingDefinitionStartupCheck>();

        services.AddDataProtection();

        services.AddScoped<ISettingValueStore, SettingValueStore>();
        services.AddScoped<ISettingValueService, SettingValueService>();
        services.AddScoped<ISettingProvider, SettingProvider>();
    }
}