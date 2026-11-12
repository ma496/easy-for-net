namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// This route group that prefixes the SEO page administration endpoints with <c>seo/pages</c>.
/// </summary>
sealed class SeoPagesGroup : Group
{
    public SeoPagesGroup()
    {
        Configure("seo/pages", ep => {});
    }
}
