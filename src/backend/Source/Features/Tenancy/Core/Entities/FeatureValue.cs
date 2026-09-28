namespace Backend.Features.Tenancy.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// One stored feature value, addressed by the provider that set it and the key that provider names.
/// </summary>
/// <remarks>
/// Only values are stored; the catalogue of features is code, declared by the
/// <see cref="IFeatureDefinitionProvider"/>s, so <see cref="Name"/> carries no foreign key and a
/// feature removed from the code leaves a row the seeder prunes on the next start.
/// <para>
/// The row carries no tenant column and no soft delete, both deliberately.
/// <see cref="ProviderName"/> and <see cref="ProviderKey"/> already name the tenant when the provider
/// is a tenant - and name an edition, which belongs to no tenant, when it is not - so a tenant column
/// would be a duplicate fact the two could disagree about. Clearing an override is deleting the row,
/// which is what makes the value fall through to the next provider; a tombstone would have to be
/// excluded from the unique index for no gain.
/// </para>
/// </remarks>
public class FeatureValue : AuditableEntity<Guid>
{
    /// <summary>
    /// The feature this value is for, as declared in code.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The value, always as text. What the text means is decided by the definition's value type.
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Which kind of thing this value was set for, from <see cref="FeatureValueProviderNames"/>.
    /// </summary>
    public string ProviderName { get; set; } = null!;

    /// <summary>
    /// Which one of them, as text - a tenant id or an edition id. Text rather than a
    /// <see cref="Guid"/> so the table stays open to a provider keyed by something else.
    /// </summary>
    public string ProviderKey { get; set; } = null!;
}
