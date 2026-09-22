namespace Backend.Features.Tenancy.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// A plan the platform sells. An edition carries a set of feature values, and every tenant on it is
/// entitled to those values unless it overrides one of them for itself.
/// </summary>
/// <remarks>
/// An edition belongs to no tenant - it is a thing the platform offers to all of them - so it carries
/// no tenant of its own and is administered in platform scope alone. It lives in the tenancy slice
/// because <see cref="Tenant.EditionId"/> is a real foreign key, and an entity referenced from another
/// slice's entity would put two slices' tables in one relationship.
/// </remarks>
public class Edition : AuditableEntity<Guid>, ISoftDelete, IHasNormalizedProperties
{
    public string Name { get; set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Description { get; set; }

    /// <summary>
    /// Where this edition sits when plans are listed, lowest first, so a catalogue can read from the
    /// cheapest plan upwards rather than alphabetically.
    /// </summary>
    public int DisplayOrder { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Recomputes <see cref="NameNormalized"/> from <see cref="Name"/> so that every uniqueness
    /// comparison, lookup and search runs against the trimmed lower-case form.
    /// </summary>
    public void NormalizeProperties()
    {
        NameNormalized = Name.Trim().ToLowerInvariant();
    }
}
