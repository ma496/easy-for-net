namespace Backend.Features.Identity.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// A named, grantable action in the system. Permissions are assigned to roles and ultimately to users through those roles.
/// </summary>
public class Permission : AuditableEntity<Guid>
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// The scope this permission may be exercised in, mirroring the code-declared catalogue. It is
    /// persisted because a session's permissions are narrowed to its scope on every request, and
    /// that narrowing runs in the database; <see cref="Backend.ShareData.DataSeeder"/> reconciles the
    /// column on every startup, so it can never drift from the definitions.
    /// </summary>
    public PermissionScope Scope { get; set; } = PermissionScope.Tenant;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}