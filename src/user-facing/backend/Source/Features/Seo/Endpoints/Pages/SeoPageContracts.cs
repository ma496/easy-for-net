using Backend.Features.Localization.Core;
using Backend.Features.Seo.Core;
using Backend.Features.Seo.Core.Entities;

namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// Every writable field of an SEO page, as <c>POST /seo/pages</c> and <c>PUT /seo/pages/{id}</c> take
/// them. Blank text is stored as no value, so the page falls back for that field.
/// </summary>
public abstract class SeoPageInput
{
    public string Path { get; set; } = null!;
    public string? Culture { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public Guid? OgImageFileId { get; set; }
    public string TwitterCard { get; set; } = SeoConstants.TwitterCardLargeImage;
    public bool NoIndex { get; set; }
    public bool NoFollow { get; set; }
    public bool IncludeInSitemap { get; set; } = true;
    public string? ChangeFrequency { get; set; }
    public decimal? Priority { get; set; }
    public string? JsonLd { get; set; }
}

/// <summary>
/// The rules both write endpoints hold an <see cref="SeoPageInput"/> to: a well-formed path, a shipped
/// culture, absolute http(s) URLs, a known card type and change frequency, a priority between 0 and 1,
/// and JSON-LD that parses as a JSON object or array.
/// </summary>
sealed class SeoPageInputValidator : AbstractValidator<SeoPageInput>
{
    public SeoPageInputValidator(ICultureCatalog cultureCatalog)
    {
        RuleFor(x => x.Path)
            .NotEmpty()
            .MaximumLength(SeoConstants.PathMaxLength)
            .Must(path => SeoValidationRules.IsValidPath(path?.Trim()))
            .WithMessage("The path must start with '/' and hold URL segments or {placeholders}, with no trailing slash.");
        RuleFor(x => x.Culture)
            .Must(culture => cultureCatalog.TryCanonicalize(culture!.Trim()) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.Culture))
            .WithMessage("The culture is not a supported language.");
        RuleFor(x => x.Title).MaximumLength(SeoConstants.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(SeoConstants.DescriptionMaxLength);
        RuleFor(x => x.Keywords).MaximumLength(SeoConstants.KeywordsMaxLength);
        RuleFor(x => x.CanonicalUrl)
            .MaximumLength(SeoConstants.UrlMaxLength)
            .Must(SeoValidationRules.IsAbsoluteHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.CanonicalUrl))
            .WithMessage("The canonical URL must be an absolute http(s) URL.");
        RuleFor(x => x.OgTitle).MaximumLength(SeoConstants.TitleMaxLength);
        RuleFor(x => x.OgDescription).MaximumLength(SeoConstants.DescriptionMaxLength);
        RuleFor(x => x.TwitterCard)
            .NotEmpty()
            .Must(card => SeoConstants.TwitterCards.Contains(card))
            .WithMessage("The Twitter card must be 'summary' or 'summary_large_image'.");
        RuleFor(x => x.ChangeFrequency)
            .Must(frequency => SeoConstants.ChangeFrequencies.Contains(frequency!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.ChangeFrequency))
            .WithMessage("The change frequency must be always, hourly, daily, weekly, monthly, yearly or never.");
        RuleFor(x => x.Priority).InclusiveBetween(0m, 1m);
        RuleFor(x => x.JsonLd)
            .MaximumLength(SeoConstants.JsonLdMaxLength)
            .Must(SeoValidationRules.IsJsonObjectOrArray)
            .When(x => !string.IsNullOrWhiteSpace(x.JsonLd))
            .WithErrorCode(ErrorCodes.InvalidJsonLd.Value)
            .WithMessage("The JSON-LD must be a JSON object or array.");
    }
}

/// <summary>Every field of one SEO page, as the admin screen edits it.</summary>
public sealed class SeoPageDto : AuditableDto<Guid>
{
    public string Path { get; set; } = null!;
    public string? Culture { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public Guid? OgImageFileId { get; set; }
    public string TwitterCard { get; set; } = null!;
    public bool NoIndex { get; set; }
    public bool NoFollow { get; set; }
    public bool IncludeInSitemap { get; set; }
    public string? ChangeFrequency { get; set; }
    public decimal? Priority { get; set; }
    public string? JsonLd { get; set; }
}

/// <summary>
/// Copies an <see cref="SeoPageInput"/> onto an <see cref="SeoPage"/>, and projects a page into its
/// <see cref="SeoPageDto"/>. The write endpoints normalize the input first.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class SeoPageMapper
{
    public partial void Update(SeoPageInput input, SeoPage entity);

    public partial SeoPageDto Map(SeoPage entity);
}

/// <summary>Helpers the write endpoints share.</summary>
static class SeoPageInputs
{
    /// <summary>
    /// Trims every text of <paramref name="input"/>, turns blank optional text into no value, and spells
    /// the culture the way it is shipped.
    /// </summary>
    public static void Normalize(SeoPageInput input, ICultureCatalog cultureCatalog)
    {
        input.Path = input.Path.Trim();
        input.Culture = Blank(input.Culture) is { } culture ? cultureCatalog.TryCanonicalize(culture) ?? culture : null;
        input.Title = Blank(input.Title);
        input.Description = Blank(input.Description);
        input.Keywords = Blank(input.Keywords);
        input.CanonicalUrl = Blank(input.CanonicalUrl);
        input.OgTitle = Blank(input.OgTitle);
        input.OgDescription = Blank(input.OgDescription);
        input.TwitterCard = input.TwitterCard.Trim();
        input.ChangeFrequency = Blank(input.ChangeFrequency);
        input.JsonLd = Blank(input.JsonLd);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
