namespace Backend.Features.Settings.Core;

/// <summary>
/// What an <see cref="ISettingDefinitionProvider"/> registers its settings with. Collects definitions
/// only: whether a name or a type is declared twice, and whether a default satisfies its validator, is
/// decided once every provider has contributed, when the catalogue is composed.
/// </summary>
[AllowOutside]
public sealed class SettingDefinitionContext
{
    /// <summary>The longest name a setting may be registered under - the width of the stored name column.</summary>
    public const int NameMaxLength = 128;

    private readonly List<SettingDefinition> _definitions = [];

    /// <summary>
    /// Registers the setting class <typeparamref name="T"/> under <paramref name="name"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The setting class. Its public read-write properties are its values - any type System.Text.Json
    /// can serialize - and its property initializers are its code default.
    /// </typeparam>
    /// <param name="name">The stable name the setting is stored and addressed under (<c>/settings/{name}</c>).</param>
    /// <param name="validator">
    /// The rules every resolved value of the setting must satisfy. The default - code or configured - is held to them at
    /// startup, and every write is held to them against the value it would produce.
    /// </param>
    /// <returns>A builder for the rest of the definition, so later options chain onto the registration.</returns>
    public SettingDefinitionBuilder<T> Add<T>(string name, IValidator<T> validator)
        where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(validator);
        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"The setting name '{name}' is longer than {NameMaxLength} characters.", nameof(name));
        }

        var definition = SettingDefinition.Create(name, validator);
        _definitions.Add(definition);
        return new SettingDefinitionBuilder<T>(definition);
    }

    /// <summary>The definitions registered so far, in registration order.</summary>
    internal IReadOnlyList<SettingDefinition> Definitions => _definitions;
}