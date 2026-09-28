namespace Backend.Permissions;

/// <summary>
/// Mutable builder context passed to <see cref="IPermissionDefinitionProvider"/>
/// implementations while they declare the permissions they contribute.
/// </summary>
/// <remarks>
/// A context is created, handed to a single provider and then discarded by
/// <see cref="PermissionDefinitionService"/>, so the catalogue it produces is fixed once the
/// provider returns and cannot be extended at run time.
/// </remarks>
public class PermissionDefinitionContext
{
    private IList<PermissionDefinition> _permissions { get; } = [];

    /// <summary>
    /// Adds a new top-level permission to the context.
    /// </summary>
    /// <param name="name">Stable permission name (e.g. <c>User.View</c>).</param>
    /// <param name="displayName">Human-readable name shown in the UI.</param>
    /// <param name="scope">
    /// The scope the permission - and, unless they state otherwise, every permission added beneath
    /// it - may be exercised in. Tenant tier unless stated.
    /// </param>
    /// <returns>The created <see cref="PermissionDefinition"/> which can be used to add child permissions.</returns>
    public PermissionDefinition AddPermission(string name, string displayName, PermissionScope scope = PermissionScope.Tenant)
    {
        var permission = new PermissionDefinition(name, displayName, scope);
        _permissions.Add(permission);
        return permission;
    }

    /// <summary>
    /// Returns a read-only view of the permissions that have been added to this context.
    /// </summary>
    /// <returns>The permissions defined so far in this context.</returns>
    public IReadOnlyList<PermissionDefinition> GetPermissions()
    {
        return _permissions.AsReadOnly();
    }
}
