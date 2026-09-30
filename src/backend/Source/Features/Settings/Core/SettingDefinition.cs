namespace Backend.Features.Settings.Core;

using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation.Results;

/// <summary>
/// One registered setting: its name, the class that carries its values, the properties that class
/// exposes as JSON, its code default, and the validator its values are held to. Built by
/// <see cref="SettingDefinitionContext.Add{T}"/>; the generic class is closed over here once, so
/// everything downstream - resolution, the endpoints - works on it without naming the type.
/// </summary>
sealed class SettingDefinition
{
    private readonly Dictionary<string, SettingPropertyDefinition> _propertiesByName;
    private readonly JsonObject _defaultValues;
    private readonly Func<object, ValidationResult> _validate;
    private readonly Func<object, CancellationToken, Task<ValidationResult>> _validateAsync;

    private SettingDefinition(
        string name,
        Type type,
        IReadOnlyList<SettingPropertyDefinition> properties,
        JsonObject defaultValues,
        Func<object, ValidationResult> validate,
        Func<object, CancellationToken, Task<ValidationResult>> validateAsync)
    {
        Name = name;
        Type = type;
        Properties = properties;
        _propertiesByName = properties.ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        _defaultValues = defaultValues;
        _validate = validate;
        _validateAsync = validateAsync;
    }

    /// <summary>The name the setting is registered, stored and addressed under.</summary>
    public string Name { get; }

    /// <summary>The setting class.</summary>
    public Type Type { get; }

    /// <summary>The setting's values, in declaration order, under their camelCase JSON names.</summary>
    public IReadOnlyList<SettingPropertyDefinition> Properties { get; }

    /// <summary>
    /// Closes the definition over <typeparamref name="T"/>: reads its read-write properties through
    /// System.Text.Json's own metadata - so a <c>[JsonIgnore]</c> or <c>[JsonPropertyName]</c> is
    /// honoured exactly as serialization would honour it - and serializes <c>new T()</c> once as the
    /// code default every resolution starts from.
    /// </summary>
    public static SettingDefinition Create<T>(string name, IValidator<T> validator)
        where T : class, new()
    {
        var typeInfo = SettingJson.Options.GetTypeInfo(typeof(T));
        var readWrite = typeInfo.Properties.Where(property => property.Get is not null && property.Set is not null).ToList();

        var defaultInstance = new T();
        var defaults = new JsonObject();
        foreach (var property in readWrite)
        {
            defaults[property.Name] = JsonSerializer.SerializeToNode(property.Get!(defaultInstance), property.PropertyType, SettingJson.Options);
        }

        return new SettingDefinition(
            name,
            typeof(T),
            [.. readWrite.Select(property => new SettingPropertyDefinition(property.Name, property.PropertyType))],
            defaults,
            instance => validator.Validate((T)instance),
            (instance, cancellationToken) => validator.ValidateAsync((T)instance, cancellationToken));
    }

    /// <summary>A fresh copy of the code default as a JSON object, one member per property.</summary>
    public JsonObject CreateDefaultValues() => (JsonObject)_defaultValues.DeepClone();

    /// <summary>Finds a property by its JSON name, matched case-insensitively.</summary>
    public SettingPropertyDefinition? FindProperty(string name) => _propertiesByName.GetValueOrDefault(name);

    /// <summary>Materializes a merged JSON object as an instance of the setting class.</summary>
    public object Materialize(JsonObject values)
        => values.Deserialize(Type, SettingJson.Options)
           ?? throw new InvalidOperationException($"The setting '{Name}' deserialized to nothing.");

    /// <summary>Holds an instance to the setting's validator, synchronously - how startup checks the default.</summary>
    public ValidationResult Validate(object instance) => _validate(instance);

    /// <summary>Holds an instance to the setting's validator - how a write checks the value it would produce.</summary>
    public Task<ValidationResult> ValidateAsync(object instance, CancellationToken cancellationToken)
        => _validateAsync(instance, cancellationToken);
}

/// <summary>
/// One value of a setting: its camelCase JSON name and the CLR type a stored or submitted value must
/// deserialize to.
/// </summary>
/// <param name="Name">The property's JSON name.</param>
/// <param name="Type">The property's type.</param>
sealed record SettingPropertyDefinition(string Name, Type Type)
{
    /// <summary>
    /// Whether <paramref name="value"/> deserializes to this property's type - a JSON <c>null</c> for a
    /// non-nullable value type, a string for a number or an object for a boolean does not.
    /// </summary>
    public bool Accepts(JsonNode? value) => TryNormalize(value, out _);

    /// <summary>
    /// Deserializes <paramref name="value"/> to this property's type and serializes it back, so what is
    /// stored holds only the declared shape - members the type does not declare are dropped and names are
    /// written camelCase. Answers <see langword="false"/> exactly when <see cref="Accepts"/> would.
    /// </summary>
    /// <param name="value">The submitted value.</param>
    /// <param name="normalized">The value re-serialized from the declared type, when it was accepted.</param>
    public bool TryNormalize(JsonNode? value, out JsonNode? normalized)
    {
        try
        {
            var typed = JsonSerializer.Deserialize(value?.ToJsonString() ?? "null", Type, SettingJson.Options);
            normalized = JsonSerializer.SerializeToNode(typed, Type, SettingJson.Options);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            normalized = null;
            return false;
        }
    }
}