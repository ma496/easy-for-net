namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>POST /roles</c> to create a new role, attributed to the tenant being
/// acted in and carrying a name no other role of that tenant has taken.
/// </summary>
/// <remarks>
/// The tenant is never part of the payload and is never read from one: the row takes its attribution
/// from the active scope as it is saved, so a role can only ever be created inside the tenant its
/// creator is acting in. A role name is unique within its tenant and nowhere wider - two tenants may
/// each keep a role called <c>Manager</c> - and the comparison ignores case and surrounding whitespace
/// because it is made on the normalized column. Deleted roles count: deleting a role retains its row,
/// so its name stays reserved within that tenant and cannot be taken by a role created afterwards. The
/// composite unique index over the tenant and the normalized name enforces the same rule in the
/// database, so two creations racing each other cannot both get past the check below. A platform
/// administrator acting in no tenant runs in platform scope, so the role they create belongs to no
/// tenant: it is a platform role, whose name is unique among the platform roles.
/// </remarks>
sealed class RoleCreateEndpoint(IRoleService roleService, AppDbContext dbContext)
    : Endpoint<RoleCreateRequest, RoleCreateResponse>
{
    // Name of the soft-delete query filter, suppressed for the duplicate-name check alone. Naming the
    // one filter leaves every other in force - the tenant filter above all, which is what confines the
    // comparison to the tenant the new role is about to be attributed to.
    private const string SoftDeleteFilterKey = "SoftDelete";

    private const string DuplicateNameMessage = "Role name already exists";

    public override void Configure()
    {
        Post("");
        Group<RolesGroup>();
        Permissions(Allow.Role_Create);
    }

    public override async Task HandleAsync(RoleCreateRequest request, CancellationToken cancellationToken)
    {
        // Normalized the way the entity normalizes the column being compared, so the question asked
        // here is the one the stored form answers. The tenant filter stays applied, so this asks only
        // about the tenant being acted in; the soft-delete filter is suppressed, so a name freed only
        // by a deletion is refused rather than handed out again.
        var normalizedName = request.Name.Trim().ToLowerInvariant();
        var nameExists = await dbContext.Roles
            .AsNoTracking()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AnyAsync(role => role.NameNormalized == normalizedName, cancellationToken);
        if (nameExists)
        {
            // Attributed to the name field so the web form can attach the message to the input the
            // caller has to change.
            ThrowError(x => x.Name, DuplicateNameMessage, ErrorCodes.RoleNameAlreadyExists);
        }

        var requestMapper = new RoleCreateRequestMapper();
        var entity = requestMapper.Map(request);
        // save entity to db - the tenant is stamped centrally on save from the active scope, so nothing
        // here names one and no payload can place a role in a tenant its author is not acting in.
        await roleService.CreateAsync(entity);
        var responseMapper = new RoleCreateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for creating a new role, supplying a name and optional description. It carries no
/// tenant: the tenant a role lands in is the one the caller is acting in, never one they name.
/// </summary>
public sealed class RoleCreateRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// FluentValidation rules requiring a non-empty, length-bounded name and a length-bounded optional description.
/// </summary>
sealed class RoleCreateValidator : Validator<RoleCreateRequest>
{
    public RoleCreateValidator()
    {
        // Add validation rules here
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(50);
        RuleFor(x => x.Description).MinimumLength(10).MaximumLength(255).When(x => !x.Description.IsNullOrEmpty());
    }
}

/// <summary>
/// Response payload returned after a successful role creation, echoing the assigned identifiers and metadata.
/// </summary>
public sealed class RoleCreateResponse : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>
/// This mapper that projects a <see cref="RoleCreateRequest"/> into a <see cref="Role"/> entity.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class RoleCreateRequestMapper
{
    public partial Role Map(RoleCreateRequest request);
}

/// <summary>
/// This mapper that projects a <see cref="Role"/> entity into a <see cref="RoleCreateResponse"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RoleCreateResponseMapper
{
    public partial RoleCreateResponse Map(Role entity);
}