namespace Backend.Features.Settings.Core;

/// <summary>
/// Every setting the slices declare, composed once from the registered
/// <see cref="ISettingDefinitionProvider"/>s and looked up by name or by class.
/// </summary>
interface ISettingDefinitionCatalogue
{
    /// <summary>Every declared setting, in provider and registration order.</summary>
    IReadOnlyList<SettingDefinition> GetAll();

    /// <summary>Looks a setting up by name, matched case-insensitively.</summary>
    /// <returns>The definition, or <see langword="null"/> when no provider declares that name.</returns>
    SettingDefinition? GetOrNull(string name);

    /// <summary>Looks a setting up by its class, refusing one no provider registered.</summary>
    /// <exception cref="InvalidOperationException">No provider registered <paramref name="type"/>.</exception>
    SettingDefinition Get(Type type);
}

/// <summary>
/// Default <see cref="ISettingDefinitionCatalogue"/>, composed in its constructor and registered as a
/// singleton: the catalogue is code, so it cannot change while the process runs.
/// </summary>
/// <remarks>
/// Composition is where a declaration is refused, and each refusal throws, so a bad declaration stops
/// the application from starting rather than surfacing on the first read: a name declared twice
/// (compared case-insensitively, as the route and the lookup compare it), a class registered twice (a
/// typed read could not tell which definition it meant), a configuration section that cannot be bound
/// to the setting class, and a default its own validator rejects - the code default, or the configured
/// default when the setting names a section the deployment supplies (every tenant without an override
/// would resolve to a value the setting itself calls invalid). A <see cref="SecretSettingAttribute"/> on a
/// property that is not a string is refused as the setting is registered, which is also composition.
/// <c>SettingDefinitionStartupCheck</c> resolves the catalogue while the host starts to make sure
/// composition happens then.
/// </remarks>
[NoDirectUse]
class SettingDefinitionCatalogue : ISettingDefinitionCatalogue
{
    private readonly IReadOnlyList<SettingDefinition> _all;
    private readonly Dictionary<string, SettingDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, SettingDefinition> _byType = [];

    public SettingDefinitionCatalogue(IEnumerable<ISettingDefinitionProvider> providers, IConfiguration configuration)
    {
        var all = new List<SettingDefinition>();
        foreach (var provider in providers)
        {
            var context = new SettingDefinitionContext();
            provider.Define(context);
            all.AddRange(context.Definitions);
        }

        foreach (var definition in all)
        {
            if (!_byName.TryAdd(definition.Name, definition))
            {
                throw new InvalidOperationException(
                    $"The setting '{definition.Name}' is declared more than once. Setting names are global and must be unique.");
            }

            if (!_byType.TryAdd(definition.Type, definition))
            {
                throw new InvalidOperationException(
                    $"The setting class '{definition.Type.FullName}' is registered as both '{_byType[definition.Type].Name}' and '{definition.Name}'. A class may back one setting only.");
            }

            var defaultKind = definition.ApplyConfiguredDefault(configuration)
                ? $"configured default (section '{definition.ConfigurationSection}')"
                : "code default";

            var result = definition.Validate(definition.Materialize(definition.CreateDefaultValues()));
            if (!result.IsValid)
            {
                var failures = string.Join("; ", result.Errors.Select(failure => $"{failure.PropertyName}: {failure.ErrorMessage}"));
                throw new InvalidOperationException(
                    $"The {defaultKind} of the setting '{definition.Name}' fails its own validator: {failures}");
            }
        }

        _all = all.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingDefinition> GetAll() => _all;

    /// <inheritdoc />
    public SettingDefinition? GetOrNull(string name) => _byName.GetValueOrDefault(name);

    /// <inheritdoc />
    public SettingDefinition Get(Type type)
        => _byType.GetValueOrDefault(type)
           ?? throw new InvalidOperationException(
               $"The setting class '{type.FullName}' is not registered. Declare it with context.Add<{type.Name}>(...) in an ISettingDefinitionProvider.");
}

/// <summary>
/// Composes the setting catalogue while the host starts, so a duplicate or an invalid default stops
/// the application from starting instead of failing the first request that reads a setting.
/// </summary>
sealed class SettingDefinitionStartupCheck(ISettingDefinitionCatalogue catalogue) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Resolving the singleton is what composes it; reading it keeps the intent visible.
        _ = catalogue.GetAll();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}