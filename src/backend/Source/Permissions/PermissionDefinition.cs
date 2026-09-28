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

    /// <summary>
    /// The features that must be enabled for this permission - and everything beneath it - to be
    /// exercisable at all.
    /// </summary>
    /// <remarks>
    /// Ignored when serialized, deliberately: the catalogue the web app receives has already had
    /// feature-disabled branches removed, so the requirement is something it never has to evaluate and
    /// the payload stays exactly as it was before features existed.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<string> RequiredFeatures { get; private set; } = [];

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

    /// <summary>
    /// Gates this permission - and every permission beneath it - on the named features, so a session
    /// acting where one of them is switched off is never minted with it and the permission is not
    /// offered on any role's surface there.
    /// </summary>
    /// <remarks>
    /// The requirement is cumulative with anything an ancestor states: all of them must be enabled.
    /// Declaring it on a group node is the usual shape, since a whole area is normally sold as one
    /// thing and restating the condition on each leaf only invites the two to disagree.
    /// <para>
    /// Stating it on a <see cref="PermissionScope.Platform"/> permission has no effect and is refused
    /// by an architecture test: platform scope is inside no tenant's plan, so there is no plan to
    /// consult. Never gate the permissions that administer the entitlement system itself, or a feature
    /// switched off could not be switched back on.
    /// </para>
    /// </remarks>
    /// <param name="featureNames">Names from <see cref="Backend.Features.Tenancy.Core.FeatureManagement.FeatureNames"/>.</param>
    /// <returns>This definition, so a declaration reads as one statement.</returns>
    public PermissionDefinition RequireFeatures(params string[] featureNames)
    {
        RequiredFeatures = [.. RequiredFeatures, .. featureNames];
        return this;
    }
}
