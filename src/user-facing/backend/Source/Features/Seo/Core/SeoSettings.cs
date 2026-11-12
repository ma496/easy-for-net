namespace Backend.Features.Seo.Core;

/// <summary>
/// The site-wide search-engine defaults, registered as the <c>Seo</c> setting by
/// <see cref="SeoSettingsProvider"/>. Every page falls back to these where it says nothing itself.
/// </summary>
public class SeoSettings
{
    /// <summary>The site's name, as social previews show it.</summary>
    public string SiteName { get; set; } = "Easy For Net";

    /// <summary>The title template, in which <c>%s</c> stands for the page's own title.</summary>
    public string TitleTemplate { get; set; } = "%s | Easy For Net";

    /// <summary>The description of a page that declares none.</summary>
    public string DefaultDescription { get; set; } = string.Empty;

    /// <summary>The stored file shared as the preview image of a page that names none; empty for none.</summary>
    public string DefaultOgImageFileId { get; set; } = string.Empty;

    /// <summary>The site's Twitter handle (<c>@site</c>); empty for none.</summary>
    public string TwitterSite { get; set; } = string.Empty;

    /// <summary>
    /// The site's absolute origin, used for canonical, image and sitemap URLs. Empty falls back to
    /// <c>Web:DefaultDomain</c>.
    /// </summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether search engines may index the site at all. Off marks every page <c>noindex</c> and empties
    /// the sitemap - what a staging deployment wants.
    /// </summary>
    public bool AllowIndexing { get; set; } = true;

    /// <summary>Raw lines appended to <c>robots.txt</c>.</summary>
    public string RobotsExtra { get; set; } = string.Empty;
}

/// <summary>FluentValidation rules for <see cref="SeoSettings"/>.</summary>
sealed class SeoSettingsValidator : AbstractValidator<SeoSettings>
{
    public SeoSettingsValidator()
    {
        RuleFor(x => x.SiteName).NotNull().MaximumLength(SeoConstants.TitleMaxLength);
        RuleFor(x => x.TitleTemplate)
            .NotEmpty()
            .MaximumLength(SeoConstants.TitleMaxLength)
            .Must(template => template.Contains("%s", StringComparison.Ordinal))
            .WithMessage("The title template must contain %s.");
        RuleFor(x => x.DefaultDescription).NotNull().MaximumLength(SeoConstants.DescriptionMaxLength);
        RuleFor(x => x.DefaultOgImageFileId)
            .NotNull()
            .Must(id => id.Length == 0 || Guid.TryParse(id, out _))
            .WithMessage("The default image must be empty or a file identifier.");
        RuleFor(x => x.TwitterSite).NotNull().MaximumLength(100);
        RuleFor(x => x.SiteUrl)
            .NotNull()
            .MaximumLength(SeoConstants.UrlMaxLength)
            .Must(url => url.Length == 0 || SeoValidationRules.IsAbsoluteHttpUrl(url))
            .WithMessage("The site URL must be empty or an absolute http(s) URL.");
        RuleFor(x => x.RobotsExtra).NotNull().MaximumLength(4000);
    }
}
