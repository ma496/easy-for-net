using Backend.ShareData.Entities.Base;

namespace Backend.Features.Localization.Core.Entities;

/// <summary>
/// Which shipped cultures the application offers, and which of them is the default. At most one row
/// exists: with none, the application offers every shipped culture with no default. The row is never
/// soft deleted - removing it reverts to the shipped set, not deleting a record that should stay
/// reserved.
/// </summary>
public class LanguageSetting : AuditableEntity<Guid>
{
    public List<string> EnabledCultures { get; set; } = [];
    public string? DefaultCulture { get; set; }
}
