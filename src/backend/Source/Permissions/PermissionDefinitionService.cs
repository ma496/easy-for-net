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
    /// <param name="includePlatformPermissions">
    /// When <c>false</c>, platform-tier permissions are left out, along with any parent node whose
    /// whole subtree is platform-tier - so a caller without platform authority is never offered a
    /// permission it could not grant through a tenant role.
    /// </param>
    /// <returns>The list of permission groups with their permissions.</returns>
    IReadOnlyList<PermissionGroupDefinition> GetPermissionGroups(bool includePlatformPermissions = true);
    /// <summary>
    /// Returns every leaf permission across all groups, regardless of nesting, both tiers included.
    /// </summary>
    /// <returns>The flattened list of leaf permissions.</returns>
    IReadOnlyList<FlattenedPermission> GetFlattenedPermissions();
    /// <summary>
    /// Returns the names of every platform-tier leaf permission, for callers that need to refuse
    /// granting one through a tenant role.
    /// </summary>
    /// <returns>The set of platform-tier permission names.</returns>
    IReadOnlySet<string> GetPlatformPermissionNames();
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
    public IReadOnlyList<PermissionGroupDefinition> GetPermissionGroups(bool includePlatformPermissions = true)
    {
        var groups = new List<PermissionGroupDefinition>();
        foreach (var provider in providers)
        {
            var context = new PermissionDefinitionContext();
            provider.Define(context);

            IReadOnlyList<PermissionDefinition> permissions = includePlatformPermissions
                ? context.GetPermissions()
                : [.. context.GetPermissions().Where(HasTenantPermission).Select(CopyTenantPermissions)];
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
    public IReadOnlySet<string> GetPlatformPermissionNames()
    {
        return GetFlattenedPermissions()
            .Where(p => p.IsPlatform)
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
                IsPlatform = permission.IsPlatform
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
    /// Tells whether a definition still yields a tenant-tier leaf once the platform tier is removed.
    /// </summary>
    private static bool HasTenantPermission(PermissionDefinition permission)
    {
        if (permission.IsPlatform)
        {
            return false;
        }
        return permission.Children.Count == 0 || permission.Children.Any(HasTenantPermission);
    }

    /// <summary>
    /// Rebuilds a definition subtree keeping only the branches that end in a tenant-tier leaf.
    /// </summary>
    private static PermissionDefinition CopyTenantPermissions(PermissionDefinition permission)
    {
        var copy = new PermissionDefinition(permission.Name, permission.DisplayName);
        CopyTenantChildren(permission, copy);
        return copy;
    }

    private static void CopyTenantChildren(PermissionDefinition source, PermissionDefinition target)
    {
        foreach (var child in source.Children.Where(HasTenantPermission))
        {
            CopyTenantChildren(child, target.AddChild(child.Name, child.DisplayName));
        }
    }

    #endregion
}

/// <summary>
/// Lightweight representation of a permission leaf, used when only the
/// name, display name and tier are needed (for example for seeding).
/// </summary>
public class FlattenedPermission
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the permission belongs to the platform tier and so can never be granted through a
    /// tenant role. Derived from the code-declared catalogue; never persisted.
    /// </summary>
    public bool IsPlatform { get; init; }
}
