using Backend.ShareData.Entities.Base;

namespace Backend.Features.Settings.Core.Entities;

/// <summary>
/// The overrides made to one registered setting. <see cref="Values"/> is a JSON object carrying only
/// the properties overridden - a property absent from it resolves to the setting's configured or
/// declared default. At most one row exists per setting. Rows are never soft deleted - resetting the
/// overrides removes the row outright so it stops participating in resolution.
/// </summary>
public class SettingValue : AuditableEntity<Guid>
{
    /// <summary>The setting's registered name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>A JSON object of only the properties overridden, stored as <c>jsonb</c>.</summary>
    public string Values { get; set; } = null!;
}
