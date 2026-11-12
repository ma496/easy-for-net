using Backend.Features.Localization.Core;
using Backend.Features.Seo.Core;

namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// This endpoint that handles <c>PUT /seo/pages/{id}</c> to replace every writable field of an SEO
/// page. Moving it onto the path and culture of another live page is refused with
/// <c>seoPageAlreadyExists</c>.
/// </summary>
sealed class SeoPageUpdateEndpoint(AppDbContext dbContext, ICultureCatalog cultureCatalog, ISeoCache seoCache)
    : Endpoint<SeoPageUpdateRequest, SeoPageDto>
{
    public override void Configure()
    {
        Put("{id}");
        Group<SeoPagesGroup>();
        Permissions(Allow.Seo_Update);
    }

    public override async Task HandleAsync(SeoPageUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SeoPages.FirstOrDefaultAsync(page => page.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            this.ThrowError(ErrorCodes.SeoPageNotFound, StatusCodes.Status404NotFound);
        }

        SeoPageInputs.Normalize(request, cultureCatalog);

        var id = entity.Id;
        var exists = await dbContext.SeoPages
            .AsNoTracking()
            .AnyAsync(page => page.Id != id && page.Path == request.Path && page.Culture == request.Culture, cancellationToken);
        if (exists)
        {
            this.ThrowError(x => x.Path, ErrorCodes.SeoPageAlreadyExists);
        }

        var mapper = new SeoPageMapper();
        mapper.Update(request, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        seoCache.Invalidate();

        await Send.ResponseAsync(mapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>Request payload naming the page (from the route) and every writable field.</summary>
public sealed class SeoPageUpdateRequest : SeoPageInput
{
    public Guid Id { get; set; }
}

/// <summary>FluentValidation rules for <see cref="SeoPageUpdateRequest"/>.</summary>
sealed class SeoPageUpdateValidator : Validator<SeoPageUpdateRequest>
{
    public SeoPageUpdateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new SeoPageInputValidator(Resolve<ICultureCatalog>()));
    }
}
