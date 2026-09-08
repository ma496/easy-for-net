namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>GET /roles/{id}</c> to return a single role with its permission list and user count.
/// </summary>
sealed class RoleGetEndpoint(IRoleService roleService) : Endpoint<RoleGetRequest, RoleGetResponse>
{
    public override void Configure()
    {
        Get("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_View);
    }

    public override async Task HandleAsync(RoleGetRequest request, CancellationToken cancellationToken)
    {
        // get entity from db
        var entity = await roleService.Roles()
            .Include(x => x.RolePermissions)
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        var responseMapper = new RoleGetResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the role to fetch by id.
/// </summary>
sealed class RoleGetRequest : BaseDto<Guid>
{
}

/// <summary>>
/// FluentValidation rules requiring a non-empty id for role retrieval.
/// </summary>
sealed class RoleGetValidator : Validator<RoleGetRequest>
{
    public RoleGetValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload containing the role's metadata, assigned permission ids, and user count.
/// </summary>
public sealed class RoleGetResponse : AuditableDto<Guid>
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
    public List<Guid> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}

/// <summary>
/// This mapper that projects a <see cref="Role"/> entity into a <see cref="RoleGetResponse"/>, collapsing <see cref="RolePermission"/> join rows into permission ids and computing the user count.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RoleGetResponseMapper
{
    [MapProperty(nameof(Role.RolePermissions), nameof(RoleGetResponse.Permissions), Use = nameof(RolePermissionsToPermissions)),
     MapProperty(nameof(Role.UserRoles.Count), nameof(RoleGetResponse.UserCount))]
    public partial RoleGetResponse Map(Role entity);

    private static List<Guid> RolePermissionsToPermissions(ICollection<RolePermission> rolePermissions)
    {
        return [.. rolePermissions.Select(x => x.PermissionId)];
    }
}


