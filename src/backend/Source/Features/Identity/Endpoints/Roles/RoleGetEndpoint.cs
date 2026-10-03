namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /roles/{id}</c> to return a single role with its permission list and user count.
/// </summary>
/// <remarks>
/// Only the roles of the tenant being acted in are readable here: a role belonging to another tenant
/// is answered with the same 404 as an identifier naming no role at all, so nothing about it - not its
/// name, not its permissions, not its existence - can be learned from this endpoint. A caller holding
/// platform account reads any tenant's role by entering that tenant, which is how it reaches one at all.
/// </remarks>
sealed class RoleGetEndpoint(IRoleService roleService, ITenantContext tenantContext) : Endpoint<RoleGetRequest, RoleGetResponse>
{
    public override void Configure()
    {
        Get("{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_View);
    }

    public override async Task HandleAsync(RoleGetRequest request, CancellationToken cancellationToken)
    {
        // The lookup narrows from the roles the caller may see rather than from every role, so the
        // tenant restriction and the missing-row case are one and the same code path: a role of another
        // tenant simply is not found, and falls into the 404 below without being distinguishable from a
        // role that never existed.
        //
        // Inside a tenant the user count leaves platform accounts out, as the tenant's user list does:
        // a platform account holding the role there is nobody the tenant administers.
        var includePlatformAccounts = tenantContext.IsPlatformScope();
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
                UserCount = x.UserRoles.Count(assignment => includePlatformAccounts || !assignment.User.IsPlatform)
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


