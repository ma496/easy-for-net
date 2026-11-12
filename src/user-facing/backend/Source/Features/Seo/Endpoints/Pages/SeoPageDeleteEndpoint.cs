using Backend.Features.Seo.Core;

namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// This endpoint that handles <c>DELETE /seo/pages/{id}</c> to remove an SEO page. Deletion is soft, and
/// frees the page's path and culture for a new page.
/// </summary>
sealed class SeoPageDeleteEndpoint(AppDbContext dbContext, ISeoCache seoCache)
    : Endpoint<SeoPageDeleteRequest, SeoPageDeleteResponse>
{
    public override void Configure()
    {
        Delete("{id}");
        Group<SeoPagesGroup>();
        Permissions(Allow.Seo_Update);
    }

    public override async Task HandleAsync(SeoPageDeleteRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SeoPages.FirstOrDefaultAsync(page => page.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            this.ThrowError(ErrorCodes.SeoPageNotFound, StatusCodes.Status404NotFound);
        }

        dbContext.SeoPages.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        seoCache.Invalidate();

        await Send.ResponseAsync(new SeoPageDeleteResponse { Success = true }, cancellation: cancellationToken);
    }
}

/// <summary>Request payload identifying the SEO page to delete by id.</summary>
sealed class SeoPageDeleteRequest : BaseDto<Guid>
{
}

/// <summary>FluentValidation rules requiring a non-empty id.</summary>
sealed class SeoPageDeleteValidator : Validator<SeoPageDeleteRequest>
{
    public SeoPageDeleteValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>Response payload indicating the outcome of the deletion.</summary>
sealed class SeoPageDeleteResponse
{
    public bool Success { get; set; }
}
