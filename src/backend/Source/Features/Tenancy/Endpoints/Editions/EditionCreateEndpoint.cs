namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;

/// <summary>
/// This endpoint that handles <c>POST /editions</c> to create a plan the platform can put tenants on.
/// </summary>
/// <remarks>
/// A new plan carries no feature values at all, so every tenant put on it falls straight through to
/// what the deployment configured and what the definitions declare. Values are set afterwards, through
/// the feature-management surface, which is what keeps creating a plan and pricing it two separate
/// decisions.
/// </remarks>
sealed class EditionCreateEndpoint(IEditionService editionService) : Endpoint<EditionCreateRequest, EditionCreateResponse>
{
    public override void Configure()
    {
        Post("");
        Group<EditionsGroup>();
        Permissions(Allow.Edition_Create);
    }

    public override async Task HandleAsync(EditionCreateRequest request, CancellationToken cancellationToken)
    {
        // Compared against the stored normalized form and deliberately counting deleted editions, so a
        // name differing only in case or freed only by deletion is still taken. The refusal is
        // attributed to the name field and nothing is persisted; the unique index on that column is
        // the backstop when two requests ask at once.
        if (await editionService.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            ThrowError(x => x.Name, IEditionService.DuplicateNameMessage, ErrorCodes.EditionNameAlreadyExists);
        }

        var requestMapper = new EditionCreateRequestMapper();
        var entity = await editionService.CreateAsync(requestMapper.Map(request), cancellationToken);

        var responseMapper = new EditionCreateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for creating an edition.
/// </summary>
public sealed class EditionCreateRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// FluentValidation rules ensuring a create-edition request supplies a usable plan name.
/// </summary>
sealed class EditionCreateValidator : Validator<EditionCreateRequest>
{
    public EditionCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().EditionName();
        RuleFor(x => x.Description).EditionDescription();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Response payload returned after a successful edition creation.
/// </summary>
public sealed class EditionCreateResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// This mapper that projects an <see cref="EditionCreateRequest"/> into an <see cref="Edition"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class EditionCreateRequestMapper
{
    public partial Edition Map(EditionCreateRequest request);
}

/// <summary>
/// This mapper that projects a created <see cref="Edition"/> into an <see cref="EditionCreateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EditionCreateResponseMapper
{
    public partial EditionCreateResponse Map(Edition entity);
}
