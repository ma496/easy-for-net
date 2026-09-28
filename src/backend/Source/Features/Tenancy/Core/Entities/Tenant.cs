namespace Backend.Features.Tenancy.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// An isolated customer of the application. Every tenant-scoped row belongs to exactly one tenant,
/// and a user reaches a tenant's data only through a <see cref="TenantMembership"/>.
/// </summary>
public class Tenant : AuditableEntity<Guid>, ISoftDelete, IHasNormalizedProperties, ISystemCreated
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; private set; } = null!;
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// The plan this tenant is on, or <see langword="null"/> when it is on none.
    /// </summary>
    /// <remarks>
    /// A tenant on no edition is legitimate, and is what a fresh installation looks like: its feature
    /// values simply fall through to what the deployment configured and what the definitions declare.
    /// </remarks>
    public Guid? EditionId { get; set; }

    public Edition? Edition { get; set; }

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
