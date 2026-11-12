using Backend.Features.Seo.Core;
using Backend.Features.Settings.Core;

namespace Backend.Features.Seo;

/// <summary>
/// Feature module that registers the SEO services with the DI container. The cache is a singleton, and
/// is also the <see cref="ISettingChangedHandler"/> that drops it when the <c>Seo</c> setting changes.
/// </summary>
[BypassNoDirectUse]
public class SeoFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<SeoCache>();
        services.AddSingleton<ISeoCache>(provider => provider.GetRequiredService<SeoCache>());
        services.AddSingleton<ISettingChangedHandler>(provider => provider.GetRequiredService<SeoCache>());
        services.AddScoped<ISeoSiteService, SeoSiteService>();
    }
}
