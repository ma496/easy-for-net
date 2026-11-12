using Backend.Features.Localization.Core;
using Backend.Features.Seo.Core;
using Backend.Features.Seo.Core.Entities;

namespace Backend.Features.Seo.Endpoints.Seo;

/// <summary>
/// This endpoint that handles <c>GET /seo/resolve?path=/about&amp;culture=en</c> to answer the metadata
/// the web app renders into one page's head.
/// </summary>
/// <remarks>
/// Anonymous: every visitor sees the same metadata. The page is found by its exact path, else by the
/// most specific template matching it (<c>/blog/{slug}</c>); within it each field is merged, first
/// answer winning, from the row for the culture asked for, then the culture-neutral row, then
/// <see cref="SeoSettings"/>. What the answer is computed from - the settings and every page of the
/// culture - is cached per shipped culture until any SEO page or the <c>Seo</c> setting changes, so the
/// cache stays bounded whatever paths anonymous callers ask for.
/// </remarks>
sealed class SeoResolveEndpoint(AppDbContext dbContext,
                                ISeoSiteService seoSiteService,
                                ISeoCache seoCache,
                                ICultureCatalog cultureCatalog) : Endpoint<SeoResolveRequest, SeoResolveResponse>
{
    public override void Configure()
    {
        Get("resolve");
        Group<SeoGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(SeoResolveRequest request, CancellationToken cancellationToken)
    {
        var path = request.Path.Trim();
        // A culture that is not shipped has no rows of its own, so it reads as the culture-neutral pages.
        var culture = string.IsNullOrWhiteSpace(request.Culture) ? null : cultureCatalog.TryCanonicalize(request.Culture.Trim());

        var data = await seoCache.GetOrCreateAsync(
            $"resolve:{culture}",
            () => LoadAsync(culture, cancellationToken));

        await Send.ResponseAsync(Resolve(data, path, culture), cancellation: cancellationToken);
    }

    /// <summary>The settings and every live page of one culture, culture-neutral pages included.</summary>
    private async Task<ResolveData> LoadAsync(string? culture, CancellationToken cancellationToken)
    {
        var settings = await seoSiteService.GetSettingsAsync(cancellationToken);
        var rows = await dbContext.SeoPages
            .AsNoTracking()
            .Where(page => page.Culture == null || page.Culture == culture)
            .Select(page => new PageRow(
                page.Path, page.Culture, page.Title, page.Description, page.Keywords, page.CanonicalUrl,
                page.OgTitle, page.OgDescription, page.OgImageFileId, page.TwitterCard,
                page.NoIndex, page.NoFollow, page.JsonLd))
            .ToListAsync(cancellationToken);

        return new ResolveData(settings, seoSiteService.SiteUrl(settings), rows);
    }

    private SeoResolveResponse Resolve(ResolveData data, string path, string? culture)
    {
        var settings = data.Settings;
        var siteUrl = data.SiteUrl;
        var candidates = data.Rows;

        var matchedPath = candidates.Any(row => row.Path == path)
            ? path
            : candidates
                .Where(row => SeoValidationRules.IsTemplate(row.Path) && SeoValidationRules.TemplateMatches(row.Path, path))
                .OrderBy(row => SeoValidationRules.PlaceholderCount(row.Path))
                .ThenBy(row => row.Path, StringComparer.Ordinal)
                .Select(row => row.Path)
                .FirstOrDefault();

        var rows = candidates.Where(row => row.Path == matchedPath).ToList();
        var cultureRow = culture is null ? null : rows.FirstOrDefault(row => row.Culture == culture);
        var neutralRow = rows.FirstOrDefault(row => row.Culture == null);
        PageRow?[] layers = [cultureRow, neutralRow];
        var primary = cultureRow ?? neutralRow;

        string? First(Func<PageRow, string?> field)
            => layers.Select(row => row is null ? null : field(row)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var imageFileId = layers.Select(row => row?.OgImageFileId).FirstOrDefault(id => id is not null)
                          ?? (Guid.TryParse(settings.DefaultOgImageFileId, out var defaultImage) ? defaultImage : null);

        return new SeoResolveResponse
        {
            Title = First(row => row.Title),
            TitleTemplate = settings.TitleTemplate,
            SiteName = settings.SiteName,
            SiteUrl = siteUrl,
            Description = First(row => row.Description) ?? NullIfEmpty(settings.DefaultDescription),
            Keywords = First(row => row.Keywords),
            CanonicalUrl = First(row => row.CanonicalUrl),
            OgTitle = First(row => row.OgTitle),
            OgDescription = First(row => row.OgDescription),
            OgImageUrl = imageFileId is { } fileId ? seoSiteService.ImageUrl(siteUrl, fileId) : null,
            TwitterCard = primary?.TwitterCard ?? SeoConstants.TwitterCardLargeImage,
            TwitterSite = NullIfEmpty(settings.TwitterSite),
            Index = settings.AllowIndexing && primary?.NoIndex != true,
            Follow = primary?.NoFollow != true,
            JsonLd = First(row => row.JsonLd)
        };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>What resolution reads, cached per culture.</summary>
    private sealed record ResolveData(SeoSettings Settings, string SiteUrl, List<PageRow> Rows);

    /// <summary>The columns resolution reads from one page row.</summary>
    private sealed record PageRow(
        string Path,
        string? Culture,
        string? Title,
        string? Description,
        string? Keywords,
        string? CanonicalUrl,
        string? OgTitle,
        string? OgDescription,
        Guid? OgImageFileId,
        string TwitterCard,
        bool NoIndex,
        bool NoFollow,
        string? JsonLd);
}

/// <summary>Request naming the route (without its locale) and, optionally, the culture it is served in.</summary>
sealed class SeoResolveRequest
{
    [QueryParam]
    public string Path { get; set; } = null!;

    [QueryParam]
    public string? Culture { get; set; }
}

/// <summary>FluentValidation rules requiring a path that starts with <c>/</c>.</summary>
sealed class SeoResolveValidator : Validator<SeoResolveRequest>
{
    public SeoResolveValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty()
            .MaximumLength(SeoConstants.PathMaxLength)
            .Must(path => path.TrimStart().StartsWith('/'))
            .WithMessage("The path must start with '/'.");
        RuleFor(x => x.Culture).MaximumLength(16);
    }
}

/// <summary>
/// The metadata of one page as the web app renders it. <see cref="Title"/> is the page's own title; the
/// web app applies <see cref="TitleTemplate"/> to it. Every URL is absolute.
/// </summary>
public sealed class SeoResolveResponse
{
    public string? Title { get; set; }
    public string TitleTemplate { get; set; } = null!;
    public string SiteName { get; set; } = null!;
    public string SiteUrl { get; set; } = null!;
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImageUrl { get; set; }
    public string TwitterCard { get; set; } = null!;
    public string? TwitterSite { get; set; }
    public bool Index { get; set; }
    public bool Follow { get; set; }
    public string? JsonLd { get; set; }
}
