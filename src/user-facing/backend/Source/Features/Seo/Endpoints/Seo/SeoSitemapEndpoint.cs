using Backend.Features.Localization.Core;
using Backend.Features.Seo.Core;

namespace Backend.Features.Seo.Endpoints.Seo;

/// <summary>
/// This endpoint that handles <c>GET /seo/sitemap</c> to answer what the web app builds
/// <c>sitemap.xml</c> from: the site's origin, its languages, and every page path to list.
/// </summary>
/// <remarks>
/// Anonymous. A path is listed when one of its live pages is included in the sitemap and not marked
/// noindex, and it is not a template. Its change frequency and priority come from its culture-neutral
/// page when that qualifies, else from the first culture page that does; its last modification is the
/// latest of them. With <see cref="SeoSettings.AllowIndexing"/> off nothing is listed.
/// </remarks>
sealed class SeoSitemapEndpoint(AppDbContext dbContext,
                                ISeoSiteService seoSiteService,
                                ILanguageProvider languageProvider) : EndpointWithoutRequest<SeoSitemapResponse>
{
    public override void Configure()
    {
        Get("sitemap");
        Group<SeoGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var settings = await seoSiteService.GetSettingsAsync(cancellationToken);
        var languages = await languageProvider.GetEnabledLanguagesAsync(cancellationToken);

        var entries = new List<SeoSitemapEntryDto>();
        if (settings.AllowIndexing)
        {
            var rows = await dbContext.SeoPages
                .AsNoTracking()
                .Where(page => page.IncludeInSitemap && !page.NoIndex && !page.Path.Contains("{"))
                .Select(page => new
                {
                    page.Path,
                    page.Culture,
                    page.ChangeFrequency,
                    page.Priority,
                    LastModified = page.UpdatedAt ?? page.CreatedAt
                })
                .ToListAsync(cancellationToken);

            entries = rows
                .GroupBy(row => row.Path, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    var primary = group.FirstOrDefault(row => row.Culture == null) ?? group.OrderBy(row => row.Culture, StringComparer.Ordinal).First();
                    return new SeoSitemapEntryDto
                    {
                        Path = group.Key,
                        LastModified = DateTime.SpecifyKind(group.Max(row => row.LastModified), DateTimeKind.Utc),
                        ChangeFrequency = primary.ChangeFrequency,
                        Priority = primary.Priority
                    };
                })
                .ToList();
        }

        await Send.ResponseAsync(new SeoSitemapResponse
        {
            SiteUrl = seoSiteService.SiteUrl(settings),
            DefaultCulture = languages.DefaultCulture,
            Cultures = [.. languages.Cultures],
            Entries = entries
        }, cancellation: cancellationToken);
    }
}

/// <summary>Everything the web app needs to write <c>sitemap.xml</c>.</summary>
public sealed class SeoSitemapResponse
{
    /// <summary>The site's absolute origin, with no trailing slash.</summary>
    public string SiteUrl { get; set; } = null!;

    /// <summary>The culture served when none is in the URL.</summary>
    public string DefaultCulture { get; set; } = null!;

    /// <summary>Every enabled culture.</summary>
    public List<string> Cultures { get; set; } = [];

    /// <summary>The page paths to list, without their locale prefix.</summary>
    public List<SeoSitemapEntryDto> Entries { get; set; } = [];
}

/// <summary>One path of the sitemap.</summary>
public sealed class SeoSitemapEntryDto
{
    public string Path { get; set; } = null!;
    public DateTime LastModified { get; set; }
    public string? ChangeFrequency { get; set; }
    public decimal? Priority { get; set; }
}
