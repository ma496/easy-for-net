namespace Backend.Features.Seo.Core;

/// <summary>
/// The fixed vocabulary and bounds of the <c>Seo</c> slice: column lengths, the Twitter card types
/// and the sitemap change frequencies a page may name.
/// </summary>
public static class SeoConstants
{
    public const int PathMaxLength = 300;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 500;
    public const int KeywordsMaxLength = 500;
    public const int UrlMaxLength = 2000;
    public const int JsonLdMaxLength = 20000;

    public const string TwitterCardSummary = "summary";
    public const string TwitterCardLargeImage = "summary_large_image";

    /// <summary>The Twitter card types a page may name.</summary>
    public static readonly IReadOnlyList<string> TwitterCards = [TwitterCardSummary, TwitterCardLargeImage];

    /// <summary>The change frequencies the sitemap protocol defines.</summary>
    public static readonly IReadOnlyList<string> ChangeFrequencies = ["always", "hourly", "daily", "weekly", "monthly", "yearly", "never"];
}
