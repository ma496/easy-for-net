namespace Backend.Features.Localization.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// One resolved translation value for a single key in a single culture, scoped to either a tenant or
/// the platform. A row with no <see cref="TenantId"/> is a platform override, visible to every caller
/// acting in no tenant and inherited by any tenant that has not overridden the same key itself. Rows
/// are never soft deleted - resetting a key to the shipped value removes the row outright so it stops
/// participating in resolution at all rather than lingering as a deleted override.
/// </summary>
public class LocalizationText : AuditableEntity<Guid>, IMayHaveTenant
{
    public Guid? TenantId { get; set; }
    public string Culture { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}
