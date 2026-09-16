namespace Backend.Data.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// An isolated customer of the application. Every tenant-scoped row belongs to exactly one tenant,
/// and a user reaches a tenant's data only through a <see cref="TenantMembership"/>.
/// </summary>
public class Tenant : AuditableEntity<Guid>, ISoftDelete, IHasNormalizedProperties
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; private set; } = null!;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Recomputes <see cref="IdentifierNormalized"/> from <see cref="Identifier"/> so that every
    /// uniqueness comparison, lookup and search runs against the trimmed lower-case form.
    /// </summary>
    public void NormalizeProperties()
    {
        IdentifierNormalized = Identifier.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Lifecycle state of a <see cref="Tenant"/>. Deletion is not a status: a deleted tenant is a
/// soft-deleted row (<see cref="Tenant.IsDeleted"/>).
/// </summary>
public enum TenantStatus
{
    Active = 1,
    Suspended = 2
}