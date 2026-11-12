using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Core.Sessions;

namespace Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// This endpoint that handles <c>PUT /roles/change-permissions/{id}</c> to replace a role's permission set in a single operation.
/// </summary>
/// <remarks>
/// A system-created role refuses permission changes outright. A change that alters what the role grants
/// ends, once saved, the sessions of every account holding the role, since each carries the permissions
/// it was minted with. Resubmitting the set the role already holds changes nothing and ends nothing.
/// </remarks>
sealed class ChangePermissionsEndpoint(
    IRoleService roleService,
    AppDbContext dbContext,
    ISessionRevocationService sessionRevocationService)
    : Endpoint<ChangePermissionsRequest, ChangePermissionsResponse>
{
    public override void Configure()
    {
        Put("change-permissions/{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_ChangePermissions);
    }

    public override async Task HandleAsync(ChangePermissionsRequest request, CancellationToken cancellationToken)
    {
        var entity = await roleService.Roles()
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            this.ThrowError(ErrorCodes.SystemCreatedRolePermissionsCannotBeChanged);

        // update role permissions based on request and already assigned permissions
        var permissionsToAssign = request.Permissions.Where(x => !entity.RolePermissions.Any(rp => rp.PermissionId == x)).ToList();
        foreach (var permission in permissionsToAssign)
        {
            entity.RolePermissions.Add(new RolePermission { PermissionId = permission });
        }
        var permissionsToRemove = entity.RolePermissions
            .Where(x => !request.Permissions.Contains(x.PermissionId))
            .ToList();
        foreach (var permission in permissionsToRemove)
        {
            entity.RolePermissions.Remove(permission);
        }

        // save entity to db
        await roleService.UpdateAsync(entity);

        if (permissionsToAssign.Count > 0 || permissionsToRemove.Count > 0)
        {
            var holderIds = await dbContext.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.RoleId == entity.Id)
                .Select(userRole => userRole.UserId)
                .ToListAsync(cancellationToken);
            await sessionRevocationService.RevokeUsersAsync(holderIds, cancellationToken);
        }
        await Send.ResponseAsync(
            new()
            {
                Id = entity.Id,
                Permissions = [.. entity.RolePermissions.Select(x => x.PermissionId)]
            }, cancellation: cancellationToken
        );
    }
}

/// <summary>
/// Request payload identifying a role and the complete set of permission ids it should have after the change.
/// </summary>
sealed class ChangePermissionsRequest : BaseDto<Guid>
{
    public List<Guid> Permissions { get; set; } = [];
}

/// <summary>
/// Response payload echoing the role's id and the resulting permission ids after the change is applied.
/// </summary>
sealed class ChangePermissionsResponse : BaseDto<Guid>
{
    public List<Guid> Permissions { get; set; } = [];
}