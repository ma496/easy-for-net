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
/// platform authority by re-permissioning one of its own roles. The converse holds too: a
/// tenant-scoped permission is exercised only through a tenant's own roles, so a platform role that
/// held one would grant nothing anywhere and is refused it.
/// <para>
/// The submitted set replaces what the role held, but only among the permissions the caller could
/// actually see. A permission the tenant's plan withholds is absent from the catalogue this form is
/// built from, so submitting the form says nothing about it - and taking that silence for "remove it"
/// would revoke the grant for good, since re-enabling the feature restores nothing that was deleted.
/// Such grants are carried through the replacement untouched.
/// </para>
/// </remarks>
sealed class ChangePermissionsEndpoint(
    IRoleService roleService,
    IPermissionService permissionService,
    IPermissionFeatureFilter permissionFeatureFilter)
    : Endpoint<ChangePermissionsRequest, ChangePermissionsResponse>
{
    private const string SystemCreatedMessage = "System-created role permissions cannot be changed";
    private const string PlatformPermissionMessage = "A platform permission cannot be granted through a tenant role";
    private const string TenantPermissionMessage = "A tenant permission cannot be granted through a platform role";

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

        await RefuseOutOfScopePermissionsAsync(entity, request.Permissions, cancellationToken);

        // update role permissions based on request and already assigned permissions
        var permissionsToAssign = request.Permissions.Where(x => !entity.RolePermissions.Any(rp => rp.PermissionId == x)).ToList();
        foreach (var permission in permissionsToAssign)
        {
            entity.RolePermissions.Add(new RolePermission { PermissionId = permission });
        }
        var withheldByPlan = await GrantsWithheldByPlanAsync(entity, cancellationToken);
        var permissionsToRemove = entity.RolePermissions
            .Where(x => !request.Permissions.Contains(x.PermissionId) && !withheldByPlan.Contains(x.PermissionId))
            .ToList();
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
    /// The role's current grants that the tenant's plan hides, and which the submitted set therefore
    /// cannot be speaking about.
    /// </summary>
    /// <param name="role">The role whose permission set is being replaced.</param>
    /// <param name="cancellationToken">Token that cancels the lookup.</param>
    /// <returns>The permission ids to carry through the replacement regardless of what was submitted.</returns>
    /// <remarks>
    /// Resolved for the role's own tenant rather than the caller's scope: a platform account editing a
    /// tenant's role is asking about that tenant's plan, not about its own standing. A platform role
    /// belongs to no tenant and no plan, so nothing is hidden from it and nothing needs carrying.
    /// </remarks>
    private async Task<HashSet<Guid>> GrantsWithheldByPlanAsync(Role role, CancellationToken cancellationToken)
    {
        if (role.TenantId is not { } tenantId || role.RolePermissions.Count == 0)
        {
            return [];
        }

        var permitted = await permissionFeatureFilter.EnabledPermissionNamesAsync(
            FeatureTarget.ForTenant(tenantId), cancellationToken);
        if (permitted is null)
        {
            return [];
        }

        var held = role.RolePermissions.Select(rolePermission => rolePermission.PermissionId).ToList();
        var names = await permissionService.Permissions()
            .AsNoTracking()
            .Where(permission => held.Contains(permission.Id))
            .Select(permission => new { permission.Id, permission.Name })
            .ToListAsync(cancellationToken);

        return [.. names.Where(permission => !permitted.Contains(permission.Name))
                        .Select(permission => permission.Id)];
    }

    /// <summary>
    /// Refuses the change when it would leave a role holding a permission that cannot be exercised in
    /// the scope the role belongs to: a platform-scoped permission on a tenant's role, or a
    /// tenant-scoped one on a platform role.
    /// </summary>
    /// <param name="role">The role whose permission set is being replaced.</param>
    /// <param name="permissionIds">The complete set of permissions the role is asked to end up with.</param>
    /// <param name="cancellationToken">Token that cancels the lookup.</param>
    /// <remarks>
    /// A permission declared for both scopes fits either role. The whole requested set is examined
    /// rather than only the additions, so an out-of-scope permission cannot survive on a role by being
    /// resubmitted along with the rest.
    /// </remarks>
    private async Task RefuseOutOfScopePermissionsAsync(Role role, List<Guid> permissionIds, CancellationToken cancellationToken)
    {
        if (permissionIds.Count == 0)
        {
            return;
        }

        var foreignScope = role.TenantId is null ? PermissionScope.Tenant : PermissionScope.Platform;
        var grantsForeignPermission = await permissionService.Permissions()
            .AsNoTracking()
            .AnyAsync(permission => permissionIds.Contains(permission.Id)
                                    && permission.Scope == foreignScope, cancellationToken);
        if (!grantsForeignPermission)
        {
            return;
        }

        // Attributed to the permissions field so the web form can attach the message to the
        // selection the caller has to correct.
        if (role.TenantId is null)
        {
            ThrowError(x => x.Permissions, TenantPermissionMessage, ErrorCodes.TenantPermissionNotGrantable);
        }

        ThrowError(x => x.Permissions, PlatformPermissionMessage, ErrorCodes.PlatformPermissionNotGrantable);
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