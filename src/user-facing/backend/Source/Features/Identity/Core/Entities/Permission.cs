using Backend.ShareData.Entities.Base;

namespace Backend.Features.Identity.Core.Entities;

/// <summary>
/// A named, grantable action in the system. Permissions are assigned to roles and ultimately to users through those roles.
/// </summary>
public class Permission : AuditableEntity<Guid>
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}