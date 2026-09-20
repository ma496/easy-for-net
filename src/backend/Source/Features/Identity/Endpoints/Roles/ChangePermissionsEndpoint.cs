namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>PUT /roles/change-permissions/{id}</c> to replace a role's permission set in a single operation.
/// </summary>
/// <remarks>
/// Only the roles of the scope being acted in can be re-permissioned here - a tenant's own inside a
/// tenant, the platform's own in platform scope. A role belonging to another tenant is answered with
/// the same 404 as an identifier naming no role at all and keeps the permissions it had, so nothing
/// about it can be learned or changed from outside its tenant. A tenant's system-created administrator
/// role refuses permission changes outright. Beyond that, a permission declares the scope it may be
/// exercised in: a platform-scoped permission governs the installation rather than any one tenant and
/// can never be granted through a role that belongs to a tenant, so no tenant can promote itself to
/// platform authority by re-permissioning one of its own roles.
/// </remarks>
sealed class ChangePermissionsEndpoint(
    IRoleService roleService,
    IPermissionService permissionService)
    : Endpoint<ChangePermissionsRequest, ChangePermissionsResponse>
{
    private const string SystemCreatedMessage = "System-created role permissions cannot be changed";
    private const string PlatformPermissionMessage = "A platform permission cannot be granted through a tenant role";

    public override void Configure()
    {
        Put("change-permissions/{id}");
        Group<RolesGroup>();
        Permissions(Allow.Role_ChangePermissions);
    }

    public override async Task HandleAsync(ChangePermissionsRequest request, CancellationToken cancellationToken)
    {
        // get entity from db - narrowed to the roles the caller may see, so a role of another tenant is
        // simply not found and falls into the 404 below.
        var entity = await roleService.Roles()
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        if (entity.SystemCreated)
            ThrowError(SystemCreatedMessage, ErrorCodes.SystemCreatedRolePermissionsCannotBeChanged);

        await RefusePlatformPermissionsAsync(entity, request.Permissions, cancellationToken);

        // update role permissions based on request and already assigned permissions
        var permissionsToAssign = request.Permissions.Where(x => !entity.RolePermissions.Any(rp => rp.PermissionId == x)).ToList();
        foreach (var permission in permissionsToAssign)
        {
            entity.RolePermissions.Add(new RolePermission { PermissionId = permission });
        }
        var permissionsToRemove = entity.RolePermissions.Where(x => !request.Permissions.Contains(x.PermissionId)).ToList();
        foreach (var permission in permissionsToRemove)
        {
            entity.RolePermissions.Remove(permission);
        }

        // save entity to db
        await roleService.UpdateAsync(entity);
        await Send.ResponseAsync(
            new()
            {
                Id = entity.Id,
                Permissions = [.. entity.RolePermissions.Select(x => x.PermissionId)]
            }, cancellation: cancellationToken
        );
    }

    /// <summary>
    /// Refuses the change when it would leave a tenant's role holding a platform-scoped permission.
    /// </summary>
    /// <param name="role">The role whose permission set is being replaced.</param>
    /// <param name="permissionIds">The complete set of permissions the role is asked to end up with.</param>
    /// <param name="cancellationToken">Token that cancels the lookup.</param>
    /// <remarks>
    /// A role that belongs to no tenant is platform scoped and may hold any scope; only a tenant's own
    /// role is held to the permissions exercisable inside a tenant. The whole requested set is examined
    /// rather than only the additions, so a platform permission cannot survive on a tenant role by being
    /// resubmitted along with the rest.
    /// </remarks>
    private async Task RefusePlatformPermissionsAsync(Role role, List<Guid> permissionIds, CancellationToken cancellationToken)
    {
        if (role.TenantId is null || permissionIds.Count == 0)
        {
            return;
        }

        var grantsPlatformPermission = await permissionService.Permissions()
            .AsNoTracking()
            .AnyAsync(permission => permissionIds.Contains(permission.Id)
                                    && permission.Scope == PermissionScope.Platform, cancellationToken);
        if (grantsPlatformPermission)
        {
            // Attributed to the permissions field so the web form can attach the message to the
            // selection the caller has to correct.
            ThrowError(x => x.Permissions, PlatformPermissionMessage, ErrorCodes.PlatformPermissionNotGrantable);
        }
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