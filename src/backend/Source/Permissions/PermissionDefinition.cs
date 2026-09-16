namespace Backend.Permissions;

using System.Text.Json.Serialization;

/// <summary>
/// Describes a single permission and supports a tree of child permissions
/// used to model hierarchies such as <c>User.View</c> nested under
/// <c>User</c>.
/// </summary>
/// <remarks>
/// The catalogue is declared in code by the <see cref="IPermissionDefinitionProvider"/>s, is the
/// same for every tenant and is rebuilt from those providers on every read, so it can neither be
/// extended nor varied per tenant at run time.
/// </remarks>
public class PermissionDefinition(string name, string displayName, bool isPlatform = false)
{
    public string Name { get; set; } = name;
    public string DisplayName { get; set; } = displayName;

    /// <summary>
    /// Marks the permission as belonging to the platform tier, which means it can never be granted
    /// through a tenant role. The flag lives in memory only - it is derived from the code-declared
    /// catalogue and is never persisted alongside the permission.
    /// </summary>
    public bool IsPlatform { get; } = isPlatform;

    [JsonIgnore]
    public PermissionDefinition? Parent { get; set; }
    public IList<PermissionDefinition> Children { get; set; } = [];

    /// <summary>
    /// Creates and attaches a child permission beneath this one.
    /// </summary>
    /// <param name="name">Stable name of the child permission.</param>
    /// <param name="displayName">Display name of the child permission.</param>
    /// <param name="isPlatform">
    /// Whether the child belongs to the platform tier. A child of a platform-tier permission is
    /// always platform-tier itself, whatever is passed here.
    /// </param>
    /// <returns>The newly created child <see cref="PermissionDefinition"/>.</returns>
    public PermissionDefinition AddChild(string name, string displayName, bool isPlatform = false)
    {
        var child = new PermissionDefinition(name, displayName, isPlatform || IsPlatform)
        {
            Parent = this
        };
        Children.Add(child);
        return child;
    }
}
