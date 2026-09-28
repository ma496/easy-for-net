namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// This endpoint that handles <c>GET /editions</c> to return a paginated and searchable list of the
/// plans the platform sells.
/// </summary>
/// <remarks>
/// Platform-scoped throughout: an edition belongs to no tenant, and what plans exist is a question
/// about the platform rather than about any one customer.
/// </remarks>
sealed class EditionListEndpoint(IEditionService editionService) : Endpoint<EditionListRequest, EditionListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<EditionsGroup>();
        Permissions(Allow.Edition_View);
    }

    public override async Task HandleAsync(EditionListRequest request, CancellationToken cancellationToken)
    {
        var query = editionService.Editions().AsNoTracking();

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Matched against the stored normalized form, which is what the search term has already
            // been reduced to.
            query = query.Where(x => EF.Functions.Like(x.NameNormalized, $"%{search}%"));
        }

        // Counted before paging, so the total describes the whole filtered set rather than the page.
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Process(request)
            .ToListAsync(cancellationToken);

        // How many tenants are on a plan is asked once for the whole page rather than row by row. A
        // plan nobody is on is absent from the result, which reads as the zero the row shows.
        var tenantCounts = await editionService.TenantCountsAsync(
            [.. items.Select(edition => edition.Id)], cancellationToken);

        var dtoMapper = new EditionListDtoMapper();
        var response = new EditionListResponse
        {
            Items = [.. items.Select(edition =>
            {
                var dto = dtoMapper.Map(edition);
                dto.TenantCount = tenantCounts.GetValueOrDefault(edition.Id);
                return dto;
            })],
            Total = total
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for the edition list endpoint, supporting free-text search and the standard
/// pagination and sort options.
/// </summary>
sealed class EditionListRequest : ListRequestDto<Guid>
{
}

/// <summary>
/// FluentValidation rules for the edition list request, inheriting the standard list-request rules
/// and whitelisting the fields the list may be sorted by.
/// </summary>
sealed class EditionListValidator : Validator<EditionListRequest>
{
    public EditionListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        // Sorting reaches the database through reflection over the entity, so a field outside this
        // whitelist has to be refused here: without the rule the request would fail as an unhandled
        // error instead of as a validation failure.
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Id", "Name", "DisplayOrder", "CreatedAt", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>
/// Response payload for the edition list endpoint, wrapping a page of <see cref="EditionListDto"/>
/// items with the total number of editions the request matched.
/// </summary>
public sealed class EditionListResponse : ListDto<EditionListDto>
{
}

/// <summary>
/// Per-row DTO representing an edition in list responses.
/// </summary>
public sealed class EditionListDto : AuditableDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// How many tenants are on this plan. Filled by the endpoint rather than the mapper, because the
    /// edition row itself knows nothing about the tenants that reference it.
    /// </summary>
    public int TenantCount { get; set; }
}

/// <summary>
/// This mapper that projects an <see cref="Edition"/> entity into an <see cref="EditionListDto"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EditionListDtoMapper
{
    [MapperIgnoreTarget(nameof(EditionListDto.TenantCount))]
    public partial EditionListDto Map(Edition entity);
}
