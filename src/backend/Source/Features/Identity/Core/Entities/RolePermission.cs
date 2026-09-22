namespace Backend.Features.Identity.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Junction entity linking a <see cref="Role"/> to a <see cref="Permission"/>, representing a role's granted permission set.
/// </summary>
public class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}