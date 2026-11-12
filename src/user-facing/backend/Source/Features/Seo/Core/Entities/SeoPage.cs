using Backend.ShareData.Entities.Base;

namespace Backend.Features.Seo.Core.Entities;

/// <summary>
/// The search-engine and social-sharing metadata of one route of the public site, for one language or
/// for every language. A route is addressed by its path without the locale prefix - <c>/</c>,
/// <c>/about</c>, or a template such as <c>/blog/{slug}</c> that stands for every path of that shape.
/// </summary>
/// <remarks>
/// A page with no <see cref="Culture"/> applies to every language; one naming a culture overrides it
/// field by field for that language. At most one live page exists per (path, culture). Every nullable
/// text left empty falls back to the culture-neutral page and then to <see cref="SeoSettings"/>.
/// </remarks>
public class SeoPage : AuditableEntity<Guid>, ISoftDelete
{
    /// <summary>The route without its locale prefix, starting with <c>/</c>.</summary>
    public string Path { get; set; } = null!;

    /// <summary>The culture the page applies to, or <see langword="null"/> for every language.</summary>
    public string? Culture { get; set; }

    /// <summary>The page title, before the site's title template is applied.</summary>
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Keywords { get; set; }

    /// <summary>The absolute canonical URL of the page.</summary>
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }

    /// <summary>The stored file shared as the page's preview image.</summary>
    public Guid? OgImageFileId { get; set; }

    /// <summary>The Twitter card type: <c>summary</c> or <c>summary_large_image</c>.</summary>
    public string TwitterCard { get; set; } = SeoConstants.TwitterCardLargeImage;
    public bool NoIndex { get; set; }
    public bool NoFollow { get; set; }
    public bool IncludeInSitemap { get; set; } = true;

    /// <summary>The sitemap change frequency: always, hourly, daily, weekly, monthly, yearly or never.</summary>
    public string? ChangeFrequency { get; set; }

    /// <summary>The sitemap priority, between 0 and 1.</summary>
    public decimal? Priority { get; set; }

    /// <summary>Structured data for the page: a JSON object or array, served as-is.</summary>
    public string? JsonLd { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
