namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;

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
        var entity = await editionService.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        var mapper = new EditionGetResponseMapper();
        var response = mapper.Map(entity);
        response.TenantCount = await editionService.TenantCountAsync(entity.Id, cancellationToken);

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

/// <summary>
/// This mapper that projects an <see cref="Edition"/> entity into an <see cref="EditionGetResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EditionGetResponseMapper
{
    [MapperIgnoreTarget(nameof(EditionGetResponse.TenantCount))]
    public partial EditionGetResponse Map(Edition entity);
}
