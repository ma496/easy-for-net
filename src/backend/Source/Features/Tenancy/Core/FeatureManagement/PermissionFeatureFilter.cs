namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Narrows the permission catalogue to what a target's plan actually entitles it to.
/// </summary>
/// <remarks>
/// This is the only place the permission-to-feature rule is written. Three callers need it and must
/// not be allowed to disagree: the claims a session is minted with, the catalogue the role permission
/// screen is built from, and the grants the account information endpoint reports to the web app. If
/// any two of those answered differently, the UI would offer something the API refuses or hide
/// something it allows.
/// </remarks>
[AllowOutside]
public interface IPermissionFeatureFilter
{
    /// <summary>
    /// The permission names exercisable for a target, once feature-disabled ones are removed.
    /// </summary>
    /// <param name="target">Who the question is about.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns>
    /// The permitted names, or <see langword="null"/> when nothing is narrowed at all - which is what
    /// the platform target returns, and means callers should not filter rather than that nothing is
    /// permitted.
    /// </returns>
    Task<IReadOnlySet<string>?> EnabledPermissionNamesAsync(FeatureTarget target, CancellationToken ct = default);

    /// <summary>
    /// Returns the permission catalogue with every branch whose features are switched off removed.
    /// </summary>
    /// <param name="groups">The catalogue, already narrowed to the caller's scope.</param>
    /// <param name="target">Who the question is about.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns>The catalogue the target may actually be granted from.</returns>
    Task<IReadOnlyList<PermissionGroupDefinition>> FilterGroupsAsync(
        IReadOnlyList<PermissionGroupDefinition> groups, FeatureTarget target, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IPermissionFeatureFilter"/> implementation, composing the code-declared
/// permission catalogue with the feature values resolved for one target.
/// </summary>
[NoDirectUse]
public class PermissionFeatureFilter(IPermissionDefinitionService permissionDefinitionService,
                                     IFeatureValueResolver featureValueResolver)
    : IPermissionFeatureFilter
{
    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>?> EnabledPermissionNamesAsync(FeatureTarget target,
                                                                         CancellationToken ct = default)
    {
        // The platform is inside nobody's plan. Resolving for it would let a value written to describe
        // what tenants get decide what the platform tier may administer, which is an accident rather
        // than a rule - and it would be circular, since the permissions that switch a feature back on
        // would be the ones hidden when it is off.
        if (target.IsPlatform)
        {
            return null;
        }

        var required = permissionDefinitionService.GetRequiredFeaturesByPermission();
        var permitted = permissionDefinitionService.GetFlattenedPermissions()
                                                   .Select(permission => permission.Name)
                                                   .ToHashSet(StringComparer.Ordinal);
        if (required.Count == 0)
        {
            return permitted;
        }

        var values = await featureValueResolver.ResolveAsync(target, ct);
        foreach (var (permission, features) in required)
        {
            if (!features.All(values.IsEnabled))
            {
                permitted.Remove(permission);
            }
        }
        return permitted;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PermissionGroupDefinition>> FilterGroupsAsync(
        IReadOnlyList<PermissionGroupDefinition> groups,
        FeatureTarget target,
        CancellationToken ct = default)
    {
        var permitted = await EnabledPermissionNamesAsync(target, ct);
        if (permitted is null)
        {
            return groups;
        }

        var filtered = new List<PermissionGroupDefinition>();
        foreach (var group in groups)
        {
            var permissions = group.Permissions
                                   .Where(permission => HasPermittedLeaf(permission, permitted))
                                   .Select(permission => CopyPermittedLeaves(permission, permitted))
                                   .ToList();
            if (permissions.Count == 0)
            {
                continue;
            }

            filtered.Add(new PermissionGroupDefinition
            {
                GroupName = group.GroupName,
                Permissions = permissions
            });
        }
        return filtered.AsReadOnly();
    }

    #region Helpers

    /// <summary>
    /// Whether a branch still ends in a leaf the target may hold. A node with children is a display
    /// grouping, so it survives exactly as long as something beneath it does.
    /// </summary>
    private static bool HasPermittedLeaf(PermissionDefinition permission, IReadOnlySet<string> permitted)
        => permission.Children.Count == 0
            ? permitted.Contains(permission.Name)
            : permission.Children.Any(child => HasPermittedLeaf(child, permitted));

    /// <summary>
    /// Rebuilds a branch keeping only the paths that end in a permitted leaf, in the same shape the
    /// scope narrowing already uses.
    /// </summary>
    private static PermissionDefinition CopyPermittedLeaves(PermissionDefinition permission,
                                                            IReadOnlySet<string> permitted)
    {
        var copy = new PermissionDefinition(permission.Name, permission.DisplayName, permission.Scope);
        CopyPermittedChildren(permission, copy, permitted);
        return copy;
    }

    private static void CopyPermittedChildren(PermissionDefinition source,
                                              PermissionDefinition target,
                                              IReadOnlySet<string> permitted)
    {
        foreach (var child in source.Children.Where(candidate => HasPermittedLeaf(candidate, permitted)))
        {
            CopyPermittedChildren(child, target.AddChild(child.Name, child.DisplayName, child.Scope), permitted);
        }
    }

    #endregion
}
