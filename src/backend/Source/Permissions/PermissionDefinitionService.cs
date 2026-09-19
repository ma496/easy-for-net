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
    /// organized by their owning group. The catalogue is code-declared and identical for every
    /// tenant.
    /// </summary>
    /// <param name="viewScope">
    /// The scope the caller is acting in. Only leaves declared for that scope or for
    /// <see cref="PermissionScope.Both"/> are returned, along with the parents that still have one -
    /// so a caller is never offered a permission that could not be exercised where they are. Left
    /// unstated the whole catalogue is returned, both tiers included, which is what seeding wants.
    /// </param>
    /// <returns>The list of permission groups with their permissions.</returns>
    IReadOnlyList<PermissionGroupDefinition> GetPermissionGroups(PermissionScope? viewScope = null);

    /// <summary>
    /// Returns every leaf permission across all groups, regardless of nesting and of scope.
    /// </summary>
    /// <returns>The flattened list of leaf permissions.</returns>
    IReadOnlyList<FlattenedPermission> GetFlattenedPermissions();

    /// <summary>
    /// Returns the names of every leaf permission exercisable in one scope - those declared for it
    /// and those declared for <see cref="PermissionScope.Both"/>. This is the set a role confined to
    /// that scope may hold.
    /// </summary>
    /// <param name="viewScope">The scope the names are wanted for.</param>
    /// <returns>The set of permission names exercisable in that scope.</returns>
    IReadOnlySet<string> GetPermissionNamesInScope(PermissionScope viewScope);
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
    public IReadOnlyList<PermissionGroupDefinition> GetPermissionGroups(PermissionScope? viewScope = null)
    {
        var groups = new List<PermissionGroupDefinition>();
        foreach (var provider in providers)
        {
            var context = new PermissionDefinitionContext();
            provider.Define(context);

            IReadOnlyList<PermissionDefinition> permissions = viewScope is not { } scope
                ? context.GetPermissions()
                : [.. context.GetPermissions().Where(permission => HasLeafInScope(permission, scope))
                                              .Select(permission => CopyLeavesInScope(permission, scope))];
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

    /// <inheritdoc/>
    public IReadOnlySet<string> GetPermissionNamesInScope(PermissionScope viewScope)
    {
        return GetFlattenedPermissions()
            .Where(p => IsInScope(p.Scope, viewScope))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    #region Helpers

    private IEnumerable<FlattenedPermission> GetPermissions(PermissionDefinition permission)
    {
        if (!permission.Children.Any())
        {
            yield return new FlattenedPermission
            {
                Name = permission.Name,
                DisplayName = permission.DisplayName,
                Scope = permission.Scope
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

    /// <summary>
    /// Whether a permission is exercisable in a scope: one declared for that scope, or one declared
    /// for both.
    /// </summary>
    private static bool IsInScope(PermissionScope scope, PermissionScope viewScope)
        => scope == viewScope || scope == PermissionScope.Both;

    /// <summary>
    /// Tells whether a definition still yields a leaf exercisable in the scope asked about.
    /// </summary>
    private static bool HasLeafInScope(PermissionDefinition permission, PermissionScope viewScope)
    {
        if (permission.Children.Count == 0)
        {
            return IsInScope(permission.Scope, viewScope);
        }
        return permission.Children.Any(child => HasLeafInScope(child, viewScope));
    }

    /// <summary>
    /// Rebuilds a definition subtree keeping only the branches that end in a leaf exercisable in the
    /// scope asked about.
    /// </summary>
    private static PermissionDefinition CopyLeavesInScope(PermissionDefinition permission, PermissionScope viewScope)
    {
        var copy = new PermissionDefinition(permission.Name, permission.DisplayName, permission.Scope);
        CopyChildrenInScope(permission, copy, viewScope);
        return copy;
    }

    private static void CopyChildrenInScope(PermissionDefinition source, PermissionDefinition target, PermissionScope viewScope)
    {
        foreach (var child in source.Children.Where(candidate => HasLeafInScope(candidate, viewScope)))
        {
            CopyChildrenInScope(child, target.AddChild(child.Name, child.DisplayName, child.Scope), viewScope);
        }
    }

    #endregion
}

/// <summary>
/// Lightweight representation of a permission leaf, used when only the
/// name, display name and scope are needed (for example for seeding).
/// </summary>
public class FlattenedPermission
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// The scope the permission may be exercised in.
    /// </summary>
    public PermissionScope Scope { get; init; }
}
