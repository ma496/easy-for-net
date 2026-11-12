namespace Backend.Features.Seo.Endpoints.Pages;

/// <summary>
/// This endpoint that handles <c>GET /seo/pages</c> to return a paginated, searchable list of the SEO
/// pages.
/// </summary>
sealed class SeoPageListEndpoint(AppDbContext dbContext) : Endpoint<SeoPageListRequest, SeoPageListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<SeoPagesGroup>();
        Permissions(Allow.Seo_View);
    }

    public override async Task HandleAsync(SeoPageListRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.SeoPages.AsNoTracking();

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Path, $"%{search}%")
                || EF.Functions.ILike(x.Title!, $"%{search}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Process(request)
            .Select(x => new SeoPageListDto
            {
                Id = x.Id,
                Path = x.Path,
                Culture = x.Culture,
                Title = x.Title,
                NoIndex = x.NoIndex,
                IncludeInSitemap = x.IncludeInSitemap,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        await Send.ResponseAsync(new SeoPageListResponse { Items = items, Total = total }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for the SEO page list, supporting search over the path and title and the standard
/// pagination/sort options.
/// </summary>
sealed class SeoPageListRequest : ListRequestDto<Guid>
{
}

/// <summary>
/// FluentValidation rules for the SEO page list request: the standard list rules and the sortable fields.
/// </summary>
sealed class SeoPageListValidator : Validator<SeoPageListRequest>
{
    public SeoPageListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Path", "Culture", "Title", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>Response payload for the SEO page list: a page of rows and the total count.</summary>
public sealed class SeoPageListResponse : ListDto<SeoPageListDto>
{
}

/// <summary>One row of the SEO page list.</summary>
public sealed class SeoPageListDto : BaseDto<Guid>
{
    public string Path { get; set; } = null!;
    public string? Culture { get; set; }
    public string? Title { get; set; }
    public bool NoIndex { get; set; }
    public bool IncludeInSitemap { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
