using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// This endpoint that handles <c>PUT /roles/{id}</c> to update a role's name and description (refusing system-created roles).
/// </summary>
/// <remarks>
/// The duplicate-name comparison ignores case and surrounding whitespace, and counts deleted roles too,
/// because deleting a role does not release its name; the unique index enforces the same rule in the
/// database.
/// <para>
/// Nothing written here alters what the role grants - permissions are the only authorization input, and
/// they are replaced through <see cref="ChangePermissionsEndpoint"/> - so an update ends no session.
/// </para>
/// </remarks>
sealed class RoleUpdateEndpoint(IRoleService roleService, AppDbContext dbContext)
    : Endpoint<RoleUpdateRequest, RoleUpdateResponse>
{
    // The soft-delete filter the duplicate-name check steps past, so a deleted role's name stays taken.
    private const string SoftDeleteFilterKey = "SoftDelete";

    public override void Configure()
    {
        Put("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_Update);
    }

    public override async Task HandleAsync(RoleUpdateRequest request, CancellationToken cancellationToken)
    {
        // The role is read before anything else is decided, so that a role the caller may not see falls
        // into the 404 below without any other check having had a chance to answer differently for it
        // than it would for a role that never existed.
        var entity = await roleService.Roles()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            this.ThrowError(ErrorCodes.SystemCreatedRoleCannotBeUpdated);

        var roleId = entity.Id;
        var normalizedName = request.Name.Trim().ToLowerInvariant();
        var nameExists = await dbContext.Roles
            .AsNoTracking()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AnyAsync(role => role.Id != roleId
                              && role.NameNormalized == normalizedName, cancellationToken);
        if (nameExists)
        {
            // Attributed to the name field so the web form can attach the message to the input the
            // caller has to change.
            this.ThrowError(x => x.Name, ErrorCodes.RoleNameAlreadyExists);
        }

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