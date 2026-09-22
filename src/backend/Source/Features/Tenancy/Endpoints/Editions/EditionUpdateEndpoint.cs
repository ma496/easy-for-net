namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /editions/{id}</c> to rename a plan or change how it is described
/// and ordered.
/// </summary>
/// <remarks>
/// What the plan is worth is not changed here: its feature values live on the feature-management
/// surface, so renaming a plan never silently alters what the tenants on it are entitled to.
/// </remarks>
sealed class EditionUpdateEndpoint(IEditionService editionService) : Endpoint<EditionUpdateRequest, EditionUpdateResponse>
{
    public override void Configure()
    {
        Put("{id}");
        Group<EditionsGroup>();
        Permissions(Allow.Edition_Update);
    }

    public override async Task HandleAsync(EditionUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await editionService.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        // The edition keeping its own name is not a clash, so it is excluded from the comparison.
        if (await editionService.NameExistsAsync(request.Name, entity.Id, cancellationToken))
        {
            ThrowError(x => x.Name, IEditionService.DuplicateNameMessage, ErrorCodes.EditionNameAlreadyExists);
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.DisplayOrder = request.DisplayOrder;
        await editionService.UpdateAsync(entity, cancellationToken);

        var mapper = new EditionUpdateResponseMapper();
        await Send.ResponseAsync(mapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for updating an edition.
/// </summary>
public sealed class EditionUpdateRequest : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// FluentValidation rules for an update-edition request, sharing the creation rule set.
/// </summary>
sealed class EditionUpdateValidator : Validator<EditionUpdateRequest>
{
    public EditionUpdateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().EditionName();
        RuleFor(x => x.Description).EditionDescription();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Response payload echoing the edition as it now stands.
/// </summary>
public sealed class EditionUpdateResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// This mapper that projects an updated <see cref="Edition"/> into an <see cref="EditionUpdateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EditionUpdateResponseMapper
{
    public partial EditionUpdateResponse Map(Edition entity);
}
