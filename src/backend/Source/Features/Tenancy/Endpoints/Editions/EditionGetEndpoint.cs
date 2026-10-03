namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /editions/{id}</c> to return one plan in detail, together with
/// how many tenants are on it.
/// </summary>
sealed class EditionGetEndpoint(IEditionService editionService) : Endpoint<EditionGetRequest, EditionGetResponse>
{
    public override void Configure()
    {
        Get("{id}");
        Group<EditionsGroup>();
        Permissions(Allow.Edition_View);
    }

    public override async Task HandleAsync(EditionGetRequest request, CancellationToken cancellationToken)
    {
        // Projected in the query, so only the columns the response reports are read.
        var response = await editionService.Editions()
            .AsNoTracking()
            .Where(edition => edition.Id == request.Id)
            .Select(edition => new EditionGetResponse
            {
                Id = edition.Id,
                CreatedAt = edition.CreatedAt,
                CreatedBy = edition.CreatedBy,
                UpdatedAt = edition.UpdatedAt,
                UpdatedBy = edition.UpdatedBy,
                Name = edition.Name,
                Description = edition.Description,
                DisplayOrder = edition.DisplayOrder
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (response == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        response.TenantCount = await editionService.TenantCountAsync(response.Id, cancellationToken);

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the edition to read.
/// </summary>
sealed class EditionGetRequest : BaseDto<Guid>
{
}

/// <summary>
/// Response payload describing one edition, including how many tenants are on it - the number that
/// decides whether it may be deleted.
/// </summary>
public sealed class EditionGetResponse : AuditableDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public int TenantCount { get; set; }
}
