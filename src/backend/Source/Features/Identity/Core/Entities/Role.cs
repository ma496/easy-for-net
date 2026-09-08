namespace Backend.Features.Identity.Core.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// A named bundle of permissions that can be assigned to one or more users, simplifying access management.
/// </summary>
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public void NormalizeProperties()
    {
        NameNormalized = Name.Trim().ToLowerInvariant();
    }
}