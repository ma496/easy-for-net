namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /roles/{id}</c> to update a role's name and description (refusing system-created roles).
/// </summary>
/// <remarks>
/// Only the roles of the tenant being acted in are reachable here: a role belonging to another tenant
/// is answered with the same 404 as an identifier naming no role at all, and is left exactly as it was,
/// so neither its existence nor its name can be learned by trying to rename it. A caller holding
/// platform administration is the one exception and reads any tenant's role - which is why the
/// duplicate-name comparison below is made against the tenant the role already belongs to rather than
/// the tenant the caller happens to be acting in. Uniqueness is per tenant, ignores case and
/// surrounding whitespace, and counts the tenant's deleted roles too, because deleting a role does not
/// release its name; the composite unique index enforces the same rule in the database.
/// </remarks>
sealed class RoleUpdateEndpoint(IRoleService roleService, AppDbContext dbContext)
    : Endpoint<RoleUpdateRequest, RoleUpdateResponse>
{
    // The two query filters the duplicate-name check has to step past, named so that every other filter
    // stays in force. Both are relaxed in a single call because both must be off at once and the check
    // supplies the tenant itself: the role's own tenant, which for a platform administrator editing
    // from outside it is not the tenant the request is being made in.
    private const string TenantFilterKey = "Tenant";
    private const string SoftDeleteFilterKey = "SoftDelete";

    private const string DuplicateNameMessage = "Role name already exists";
    private const string SystemCreatedMessage = "System-created role cannot be updated";

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
            ThrowError(SystemCreatedMessage, ErrorCodes.SystemCreatedRoleCannotBeUpdated);

        // Compared within the role's own tenant, against its deleted roles as well as its live ones.
        // The tenant is named in the predicate rather than left to the filter, so the answer is the same
        // whoever asks - a platform administrator renaming another tenant's role is held to that
        // tenant's names, not to the ones of the tenant they are acting in.
        var roleId = entity.Id;
        var tenantId = entity.TenantId;
        var normalizedName = request.Name.Trim().ToLowerInvariant();
        var nameExists = await dbContext.Roles
            .AsNoTracking()
            .IgnoreQueryFilters([TenantFilterKey, SoftDeleteFilterKey])
            .AnyAsync(role => role.Id != roleId
                              && role.TenantId == tenantId
                              && role.NameNormalized == normalizedName, cancellationToken);
        if (nameExists)
        {
            // Attributed to the name field so the web form can attach the message to the input the
            // caller has to change.
            ThrowError(x => x.Name, DuplicateNameMessage, ErrorCodes.RoleNameAlreadyExists);
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