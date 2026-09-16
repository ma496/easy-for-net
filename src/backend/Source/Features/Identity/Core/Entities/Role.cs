namespace Backend.Features.Identity.Core.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// A named bundle of permissions that can be assigned to one or more users, simplifying access management.
/// A role belongs to at most one tenant: a role that names a tenant is that tenant's alone, while a role
/// with no <see cref="TenantId"/> is platform scoped and is never visible from inside a tenant. Roles are
/// soft deleted so that a deleted role's name stays reserved within its tenant.
/// </summary>
public class Role : AuditableEntity<Guid>, IHasNormalizedProperties, ISoftDelete, ITenantScoped
{
    public Guid? TenantId { get; set; }
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