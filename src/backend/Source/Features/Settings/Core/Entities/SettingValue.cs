namespace Backend.Features.Settings.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// The overrides one scope has made to one registered setting. A row with no <see cref="TenantId"/>
/// holds the platform's overrides, inherited by every tenant that has not overridden the same property
/// itself; a row naming a tenant holds that tenant's own. <see cref="Values"/> is a JSON object carrying
/// only the properties this scope overrode - a property absent from it resolves from the next scope
/// down (the platform row, then the setting's configured or declared default). At most one row exists
/// per scope and setting. Rows are never soft deleted - resetting a scope's overrides removes the row
/// outright so it stops participating in resolution rather than lingering as a deleted override.
/// </summary>
public class SettingValue : AuditableEntity<Guid>, IMayHaveTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>The setting's registered name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>A JSON object of only the properties this scope overrode, stored as <c>jsonb</c>.</summary>
    public string Values { get; set; } = null!;
}
