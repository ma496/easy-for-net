using Backend.Features.Localization.Core;
using Backend.Features.Seo.Core;
using Backend.Features.Seo.Core.Entities;

namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// This endpoint that handles <c>POST /seo/pages</c> to create an SEO page. A live page for the same
/// path and culture is refused with <c>seoPageAlreadyExists</c>.
/// </summary>
sealed class SeoPageCreateEndpoint(AppDbContext dbContext, ICultureCatalog cultureCatalog, ISeoCache seoCache)
    : Endpoint<SeoPageCreateRequest, SeoPageDto>
{
    public override void Configure()
    {
        Post("");
        Group<SeoPagesGroup>();
        Permissions(Allow.Seo_Update);
    }

    public override async Task HandleAsync(SeoPageCreateRequest request, CancellationToken cancellationToken)
    {
        SeoPageInputs.Normalize(request, cultureCatalog);

        var exists = await dbContext.SeoPages
            .AsNoTracking()
            .AnyAsync(page => page.Path == request.Path && page.Culture == request.Culture, cancellationToken);
        if (exists)
        {
            this.ThrowError(x => x.Path, ErrorCodes.SeoPageAlreadyExists);
        }

        var mapper = new SeoPageMapper();
        var entity = new SeoPage();
        mapper.Update(request, entity);
        dbContext.SeoPages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        seoCache.Invalidate();

        await Send.ResponseAsync(mapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>Request payload carrying every writable field of the new page.</summary>
public sealed class SeoPageCreateRequest : SeoPageInput
{
}

/// <summary>FluentValidation rules for <see cref="SeoPageCreateRequest"/>.</summary>
sealed class SeoPageCreateValidator : Validator<SeoPageCreateRequest>
{
    public SeoPageCreateValidator()
    {
        Include(new SeoPageInputValidator(Resolve<ICultureCatalog>()));
    }
}
