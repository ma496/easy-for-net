namespace Backend.Permissions;

/// <summary>
/// Implemented by feature modules to declare the set of permissions they
/// own. Each provider contributes one logical group of related permissions
/// to the central catalog.
/// </summary>
public interface IPermissionDefinitionProvider
{
    string GroupName { get; }
    
    /// <summary>
    /// Adds the provider's permissions to <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The builder context to populate.</param>
    void Define(PermissionDefinitionContext context);
}
