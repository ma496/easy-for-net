using Backend.Features.Identity.Core;

namespace Backend.Features.Identity.Endpoints.Roles;

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
        var response = await roleService.Roles()
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new RoleGetResponse
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                UpdatedAt = x.UpdatedAt,
                UpdatedBy = x.UpdatedBy,
                SystemCreated = x.SystemCreated,
                Name = x.Name,
                NameNormalized = x.NameNormalized,
                Description = x.Description,
                Permissions = x.RolePermissions.Select(rolePermission => rolePermission.PermissionId).ToList(),
                UserCount = x.UserRoles.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (response == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the role to fetch by id.
/// </summary>
sealed class RoleGetRequest : BaseDto<Guid>
{
}

/// <summary>
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
public sealed class RoleGetResponse : AuditableDto<Guid>, ISystemCreatedDto
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
    public List<Guid> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}


