namespace Backend.Features.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// Feature module that registers the localization services with the DI container. The resource store
/// is a singleton - the shipped locale files never change while the process runs, so they are parsed
/// once - while the resolution service is scoped, because it reads the acting tenant and the database
/// through <see cref="AppDbContext"/>, both of which are themselves scoped per request or job.
/// </summary>
[BypassNoDirectUse]
public class LocalizationFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddSingleton<ILocalizationResourceStore, LocalizationResourceStore>();
        services.AddScoped<ILocalizationService, LocalizationService>();
    }
}
