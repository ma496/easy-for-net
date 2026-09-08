namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /roles/{id}</c> to update a role's name and description (refusing system-created roles).
/// </summary>
sealed class RoleUpdateEndpoint(IRoleService roleService, AppDbContext dbContext)
    : Endpoint<RoleUpdateRequest, RoleUpdateResponse>
{
    public override void Configure()
    {
        Put("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_Update);
    }

    public override async Task HandleAsync(RoleUpdateRequest request, CancellationToken cancellationToken)
    {
        var nameExists = await dbContext.Roles
            .AnyAsync(x => x.Id != request.Id && x.NameNormalized == request.Name.Trim().ToLowerInvariant(), cancellationToken);
        if (nameExists)
        {
            ThrowError("Role name already exists", ErrorCodes.RoleNameAlreadyExists);
        }

        // get entity from db
        var entity = await roleService.Roles()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError("System-created role cannot be updated", ErrorCodes.SystemCreatedRoleCannotBeUpdated);

        var requestMapper = new RoleUpdateRequestMapper();
        requestMapper.Update(request, entity);

        // save entity to db
        await roleService.UpdateAsync(entity);
        var responseMapper = new RoleUpdateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for updating an existing role's name and description.
/// </summary>
public sealed class RoleUpdateRequest : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// FluentValidation rules for a role update request, requiring a non-empty name and length-bounded description.
/// </summary>
sealed class RoleUpdateValidator : Validator<RoleUpdateRequest>
{
    public RoleUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(50);
        RuleFor(x => x.Description).MinimumLength(10).MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Description));
    }
}

/// <summary>
/// Response payload returned after a successful role update, echoing the role's id and updated fields.
/// </summary>
public sealed class RoleUpdateResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// This mapper that updates a <see cref="Role"/> entity in-place from a <see cref="RoleUpdateRequest"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class RoleUpdateRequestMapper
{
    public partial void Update(RoleUpdateRequest request, Role entity);
}

/// <summary>
/// This mapper that projects a <see cref="Role"/> entity into a <see cref="RoleUpdateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RoleUpdateResponseMapper
{
    public partial RoleUpdateResponse Map(Role entity);
}


