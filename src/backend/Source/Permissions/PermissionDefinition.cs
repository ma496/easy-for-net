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
public class PermissionDefinition(string name, string displayName, PermissionScope scope = PermissionScope.Tenant)
{
    public string Name { get; set; } = name;
    public string DisplayName { get; set; } = displayName;

    /// <summary>
    /// The scope this permission may be exercised in. Only leaves carry a meaningful scope: a parent
    /// node is a display grouping, and is kept or pruned according to the leaves beneath it.
    /// </summary>
    public PermissionScope Scope { get; } = scope;

    [JsonIgnore]
    public PermissionDefinition? Parent { get; set; }
    public IList<PermissionDefinition> Children { get; set; } = [];

    /// <summary>
    /// Creates and attaches a child permission beneath this one.
    /// </summary>
    /// <param name="name">Stable name of the child permission.</param>
    /// <param name="displayName">Display name of the child permission.</param>
    /// <param name="scope">
    /// The scope the child may be exercised in. Left unstated the child takes its parent's scope, so
    /// a group declared for one tier does not quietly acquire a child of another.
    /// </param>
    /// <returns>The newly created child <see cref="PermissionDefinition"/>.</returns>
    public PermissionDefinition AddChild(string name, string displayName, PermissionScope? scope = null)
    {
        var child = new PermissionDefinition(name, displayName, scope ?? Scope)
        {
            Parent = this
        };
        Children.Add(child);
        return child;
    }
}
