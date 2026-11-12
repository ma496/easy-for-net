using Backend.Features.Settings.Core;
using Backend.Settings;

namespace Backend.Features.Seo.Core;

/// <summary>
/// What every public SEO endpoint needs before it answers: the <see cref="SeoSettings"/> standing now,
/// the site's absolute origin, and how a stored preview image is addressed on it.
/// </summary>
public interface ISeoSiteService
{
    /// <summary>Reads the site-wide SEO settings.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<SeoSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The site's absolute origin with no trailing slash: <see cref="SeoSettings.SiteUrl"/> when set,
    /// else <c>Web:DefaultDomain</c>.
    /// </summary>
    /// <param name="settings">The settings just read.</param>
    string SiteUrl(SeoSettings settings);

    /// <summary>The absolute URL the public image endpoint serves <paramref name="fileId"/> at.</summary>
    /// <param name="siteUrl">The site's absolute origin, as <see cref="SiteUrl"/> answers it.</param>
    /// <param name="fileId">The stored file.</param>
    string ImageUrl(string siteUrl, Guid fileId);
}

/// <summary>Default <see cref="ISeoSiteService"/>.</summary>
[NoDirectUse]
public sealed class SeoSiteService(ISettingProvider settingProvider,
                                   IOptions<WebSetting> webSetting,
                                   IConfiguration configuration) : ISeoSiteService
{
    /// <inheritdoc />
    public Task<SeoSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => settingProvider.GetAsync<SeoSettings>(cancellationToken);

    /// <inheritdoc />
    public string SiteUrl(SeoSettings settings)
        => (string.IsNullOrWhiteSpace(settings.SiteUrl) ? webSetting.Value.DefaultDomain : settings.SiteUrl).Trim().TrimEnd('/');

    /// <inheritdoc />
    public string ImageUrl(string siteUrl, Guid fileId)
    {
        // The browser reaches the API under the route prefix on the site's own origin, so the image is
        // addressed there - the same place every other API call of the web app goes.
        var routePrefix = (configuration["RoutePrefix"] ?? string.Empty).Trim('/');
        var prefix = routePrefix.Length == 0 ? string.Empty : $"/{routePrefix}";
        return $"{siteUrl}{prefix}/seo/images/{fileId:D}";
    }
}
