namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// This endpoint that handles <c>GET /seo/pages/{id}</c> to return every field of one SEO page.
/// </summary>
sealed class SeoPageGetEndpoint(AppDbContext dbContext) : Endpoint<SeoPageGetRequest, SeoPageDto>
{
    public override void Configure()
    {
        Get("{id}");
        Group<SeoPagesGroup>();
        Permissions(Allow.Seo_View);
    }

    public override async Task HandleAsync(SeoPageGetRequest request, CancellationToken cancellationToken)
    {
        var response = await dbContext.SeoPages
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new SeoPageDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                UpdatedAt = x.UpdatedAt,
                UpdatedBy = x.UpdatedBy,
                Path = x.Path,
                Culture = x.Culture,
                Title = x.Title,
                Description = x.Description,
                Keywords = x.Keywords,
                CanonicalUrl = x.CanonicalUrl,
                OgTitle = x.OgTitle,
                OgDescription = x.OgDescription,
                OgImageFileId = x.OgImageFileId,
                TwitterCard = x.TwitterCard,
                NoIndex = x.NoIndex,
                NoFollow = x.NoFollow,
                IncludeInSitemap = x.IncludeInSitemap,
                ChangeFrequency = x.ChangeFrequency,
                Priority = x.Priority,
                JsonLd = x.JsonLd
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (response is null)
        {
            this.ThrowError(ErrorCodes.SeoPageNotFound, StatusCodes.Status404NotFound);
        }

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>Request payload identifying the SEO page to fetch by id.</summary>
sealed class SeoPageGetRequest : BaseDto<Guid>
{
}

/// <summary>FluentValidation rules requiring a non-empty id.</summary>
sealed class SeoPageGetValidator : Validator<SeoPageGetRequest>
{
    public SeoPageGetValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
