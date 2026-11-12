using Backend.ShareData.Entities.Base;

namespace Backend.Features.Localization.Core.Entities;

/// <summary>
/// One override of a translation value for a single key in a single culture. Rows are never soft
/// deleted - resetting a key to the shipped value removes the row outright so it stops participating in
/// resolution at all rather than lingering as a deleted override.
/// </summary>
public class LocalizationText : AuditableEntity<Guid>
{
    public string Culture { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}
