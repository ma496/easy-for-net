using Backend.Features.Seo.Core;

namespace Backend.Features.Seo.Endpoints.Seo;

/// <summary>
/// This endpoint that handles <c>GET /seo/robots</c> to answer what the web app builds
/// <c>robots.txt</c> from.
/// </summary>
/// <remarks>Anonymous.</remarks>
sealed class SeoRobotsEndpoint(ISeoSiteService seoSiteService) : EndpointWithoutRequest<SeoRobotsResponse>
{
    public override void Configure()
    {
        Get("robots");
        Group<SeoGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var settings = await seoSiteService.GetSettingsAsync(cancellationToken);

        await Send.ResponseAsync(new SeoRobotsResponse
        {
            SiteUrl = seoSiteService.SiteUrl(settings),
            AllowIndexing = settings.AllowIndexing,
            Extra = settings.RobotsExtra
        }, cancellation: cancellationToken);
    }
}

/// <summary>The site's origin, whether it may be indexed, and the raw lines appended to robots.txt.</summary>
public sealed class SeoRobotsResponse
{
    public string SiteUrl { get; set; } = null!;
    public bool AllowIndexing { get; set; }
    public string Extra { get; set; } = null!;
}
