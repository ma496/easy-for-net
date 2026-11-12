namespace Backend.Permissions;

/// <summary>
/// Service that aggregates permission definitions from all registered
/// <see cref="IPermissionDefinitionProvider"/>s and exposes them either
/// grouped or flattened.
/// </summary>
public interface IPermissionDefinitionService
{
    /// <summary>
    /// Returns the permissions contributed by every registered provider,
    /// organized by their owning group.
    /// </summary>
    /// <returns>The list of permission groups with their permissions.</returns>
    IReadOnlyList<PermissionGroupDefinition> GetPermissionGroups();

    /// <summary>
    /// Returns every leaf permission across all groups, regardless of nesting.
    /// </summary>
    /// <returns>The flattened list of leaf permissions.</returns>
    IReadOnlyList<FlattenedPermission> GetFlattenedPermissions();
}

/// <summary>
/// Default <see cref="IPermissionDefinitionService"/> implementation that
/// composes permissions from all <see cref="IPermissionDefinitionProvider"/>s
/// registered in the DI container.
/// </summary>
[NoDirectUse]
public class PermissionDefinitionService(IEnumerable<IPermissionDefinitionProvider> providers) : IPermissionDefinitionService
{
    /// <inheritdoc/>
    public IReadOnlyList<PermissionGroupDefinition> GetPermissionGroups()
    {
        var groups = new List<PermissionGroupDefinition>();
        foreach (var provider in providers)
        {
            var context = new PermissionDefinitionContext();
            provider.Define(context);

            var permissions = context.GetPermissions();
            if (permissions.Count == 0)
            {
                continue;
            }

            groups.Add(new PermissionGroupDefinition
            {
                GroupName = provider.GroupName,
                Permissions = permissions
            });
        }
        return groups.AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<FlattenedPermission> GetFlattenedPermissions()
    {
        var allPermissions = GetPermissionGroups().SelectMany(g => g.Permissions);
        return [.. allPermissions.SelectMany(GetPermissions)];
    }

    private static IEnumerable<FlattenedPermission> GetPermissions(PermissionDefinition permission)
    {
        if (!permission.Children.Any())
        {
            yield return new FlattenedPermission
            {
                Name = permission.Name,
                DisplayName = permission.DisplayName
            };
            yield break;
        }

        foreach (var child in permission.Children)
        {
            foreach (var flattenedPermission in GetPermissions(child))
            {
                yield return flattenedPermission;
            }
        }
    }
}

/// <summary>
/// Lightweight representation of a permission leaf, used when only the
/// name and display name are needed (for example for seeding).
/// </summary>
public class FlattenedPermission
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
