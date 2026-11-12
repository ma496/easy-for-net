using Backend.ShareData.Entities.Base;

namespace Backend.Features.Identity.Core.Entities;

/// <summary>
/// A named bundle of permissions that can be assigned to one or more users, simplifying access management.
/// Roles are soft deleted so that a deleted role's name stays reserved.
/// </summary>
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties, ISoftDelete, ISystemCreated
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public void NormalizeProperties()
    {
        NameNormalized = Name.Trim().ToLowerInvariant();
    }
}