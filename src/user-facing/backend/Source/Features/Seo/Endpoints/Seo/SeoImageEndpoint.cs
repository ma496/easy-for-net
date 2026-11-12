using Backend.Features.FileManagement.Core;
using Backend.Features.Seo.Core;

namespace Backend.Features.Seo.Endpoints.Seo;

/// <summary>
/// This endpoint that handles <c>GET /seo/images/{fileId}</c> to stream a page's preview image to
/// anyone - a social network's crawler included.
/// </summary>
/// <remarks>
/// Anonymous, and therefore narrow: a stored file is served only while a live SEO page names it as its
/// image or the <c>Seo</c> setting names it as the default image. Any other identifier - an existing
/// file included - answers 404, so this never becomes a way to read the application's files.
/// </remarks>
sealed class SeoImageEndpoint(AppDbContext dbContext,
                              ISeoSiteService seoSiteService,
                              IFileService fileService) : Endpoint<SeoImageRequest>
{
    public override void Configure()
    {
        Get("images/{fileId}");
        Group<SeoGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(SeoImageRequest request, CancellationToken cancellationToken)
    {
        var settings = await seoSiteService.GetSettingsAsync(cancellationToken);
        var isDefaultImage = Guid.TryParse(settings.DefaultOgImageFileId, out var defaultImage) && defaultImage == request.FileId;
        var isReferenced = isDefaultImage
                           || await dbContext.SeoPages.AnyAsync(page => page.OgImageFileId == request.FileId, cancellationToken);
        if (!isReferenced)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        var file = await fileService.OpenPublicAsync(request.FileId, cancellationToken);
        if (file.Status != FileAccessStatus.Granted)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
        await Send.StreamAsync(file.Content, contentType: file.ContentType, cancellation: cancellationToken);
    }
}

/// <summary>Request naming the stored file to serve.</summary>
sealed class SeoImageRequest
{
    public Guid FileId { get; set; }
}

/// <summary>FluentValidation rules requiring a file identifier.</summary>
sealed class SeoImageValidator : Validator<SeoImageRequest>
{
    public SeoImageValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
    }
}
