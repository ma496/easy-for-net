namespace Backend.Features.Seo.Endpoints.Seo;

/// <summary>
/// This route group that prefixes the public SEO endpoints with the <c>seo</c> segment.
/// </summary>
sealed class SeoGroup : Group
{
    public SeoGroup()
    {
        Configure("seo", ep => {});
    }
}
