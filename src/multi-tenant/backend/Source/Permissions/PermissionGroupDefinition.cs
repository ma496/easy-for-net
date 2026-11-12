namespace Backend.Permissions;

/// <summary>
/// Bundles a set of related <see cref="PermissionDefinition"/>s under a
/// common <see cref="GroupName"/> for display and lookup purposes.
/// </summary>
public class PermissionGroupDefinition
{
    public string GroupName { get; set; } = string.Empty;
    public IReadOnlyList<PermissionDefinition> Permissions { get; set; } = [];
}
