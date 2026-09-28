namespace Backend.Features.Localization.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Which shipped cultures a tenant - or the platform, when <see cref="TenantId"/> is null - offers,
/// and which of them is the default. At most one row exists per scope: a tenant with no row of its own
/// inherits the platform row, and the platform with no row inherits the shipped culture set with no
/// default. Rows are never soft deleted - removing one is reverting the scope to what it inherits, not
/// deleting a record that should stay reserved.
/// </summary>
public class LanguageSetting : AuditableEntity<Guid>, IMayHaveTenant
{
    public Guid? TenantId { get; set; }
    public List<string> EnabledCultures { get; set; } = [];
    public string? DefaultCulture { get; set; }
}
