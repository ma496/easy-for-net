namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Service that aggregates feature definitions from all registered
/// <see cref="IFeatureDefinitionProvider"/>s and exposes them grouped, flattened or by name.
/// </summary>
[AllowOutside]
public interface IFeatureDefinitionService
{
    /// <summary>
    /// Returns the features contributed by every registered provider, organized by their owning group.
    /// The catalogue is code-declared and identical for every tenant; only the values differ.
    /// </summary>
    /// <returns>The list of feature groups with their features.</returns>
    IReadOnlyList<FeatureGroupDefinition> GetGroups();

    /// <summary>
    /// Returns every feature across all groups, parents included.
    /// </summary>
    /// <remarks>
    /// This is where features part company with permissions: a permission node that has children is a
    /// display grouping and is never granted, whereas a parent feature is itself a real feature with a
    /// value of its own. Flattening therefore keeps every node rather than only the leaves.
    /// </remarks>
    /// <returns>The flattened list of features.</returns>
    IReadOnlyList<FeatureDefinition> GetAll();

    /// <summary>
    /// Looks a feature up by name.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <returns>The definition, or <see langword="null"/> when no provider declares that name.</returns>
    FeatureDefinition? GetOrNull(string name);

    /// <summary>
    /// Looks a feature up by name, refusing an undeclared one.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="FeatureNotDefinedException">No provider declares that name.</exception>
    FeatureDefinition Get(string name);

    /// <summary>
    /// Returns the names of every declared feature, which is what the seeder compares stored values
    /// against when it prunes the ones nothing declares any more.
    /// </summary>
    /// <returns>The set of declared feature names.</returns>
    IReadOnlySet<string> GetNames();
}

/// <summary>
/// Default <see cref="IFeatureDefinitionService"/> implementation that composes the catalogue from
/// every <see cref="IFeatureDefinitionProvider"/> registered in the DI container.
/// </summary>
/// <remarks>
/// Composed once, in the constructor, and registered as a singleton: the catalogue is code, so it
/// cannot change while the process runs, and it sits on the path of every session mint and every
/// feature check. This is the one place it differs from
/// <see cref="Backend.Permissions.PermissionDefinitionService"/>, which rebuilds per call because it is
/// asked far less often.
/// </remarks>
[NoDirectUse]
public class FeatureDefinitionService : IFeatureDefinitionService
{
    private readonly IReadOnlyList<FeatureGroupDefinition> _groups;
    private readonly IReadOnlyList<FeatureDefinition> _all;
    private readonly IReadOnlyDictionary<string, FeatureDefinition> _byName;

    public FeatureDefinitionService(IEnumerable<IFeatureDefinitionProvider> providers)
    {
        var groups = new List<FeatureGroupDefinition>();
        foreach (var provider in providers)
        {
            var context = new FeatureDefinitionContext();
            provider.Define(context);

            var features = context.GetFeatures();
            if (features.Count == 0)
            {
                continue;
            }

            groups.Add(new FeatureGroupDefinition
            {
                GroupName = provider.GroupName,
                DisplayName = provider.GroupDisplayName,
                Features = features
            });
        }

        _groups = groups.AsReadOnly();
        _all = [.. _groups.SelectMany(group => group.Features).SelectMany(Flatten)];

        var byName = new Dictionary<string, FeatureDefinition>(StringComparer.Ordinal);
        foreach (var feature in _all)
        {
            if (!byName.TryAdd(feature.Name, feature))
            {
                throw new InvalidOperationException(
                    $"The feature '{feature.Name}' is declared more than once. Feature names are global and must be unique.");
            }
        }
        _byName = byName;
    }

    /// <inheritdoc/>
    public IReadOnlyList<FeatureGroupDefinition> GetGroups() => _groups;

    /// <inheritdoc/>
    public IReadOnlyList<FeatureDefinition> GetAll() => _all;

    /// <inheritdoc/>
    public FeatureDefinition? GetOrNull(string name) => _byName.GetValueOrDefault(name);

    /// <inheritdoc/>
    public FeatureDefinition Get(string name)
        => GetOrNull(name) ?? throw new FeatureNotDefinedException(name);

    /// <inheritdoc/>
    public IReadOnlySet<string> GetNames() => _byName.Keys.ToHashSet(StringComparer.Ordinal);

    #region Helpers

    /// <summary>
    /// Yields a feature and every feature beneath it, depth first, so a caller sees the whole tree as
    /// a flat sequence without losing the order a provider declared.
    /// </summary>
    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature)
    {
        yield return feature;
        foreach (var descendant in feature.Children.SelectMany(Flatten))
        {
            yield return descendant;
        }
    }

    #endregion
}
