namespace Backend.Features.Settings.Core;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using FluentValidation.Results;

/// <summary>
/// One registered setting: its name, the class that carries its values, the properties that class
/// exposes as JSON, its default, and the validator its values are held to. Built by
/// <see cref="SettingDefinitionContext.Add{T}"/>; the generic class is closed over here once, so
/// everything downstream - resolution, the endpoints - works on it without naming the type.
/// </summary>
/// <remarks>
/// The default starts as the code default - the class's property initializers - and, when the setting
/// names a configuration section (<see cref="SettingDefinitionBuilder{T}.FromConfiguration"/>) and the
/// deployment supplies it, becomes the configured default once the catalogue is composed.
/// </remarks>
sealed class SettingDefinition
{
    private readonly Dictionary<string, SettingPropertyDefinition> _propertiesByName;
    private readonly Func<object> _createInstance;
    private readonly IReadOnlyList<JsonPropertyInfo> _jsonProperties;
    private readonly Func<object, ValidationResult> _validate;
    private readonly Func<object, CancellationToken, Task<ValidationResult>> _validateAsync;
    private JsonObject _defaultValues;

    private SettingDefinition(
        string name,
        Type type,
        IReadOnlyList<SettingPropertyDefinition> properties,
        Func<object> createInstance,
        IReadOnlyList<JsonPropertyInfo> jsonProperties,
        Func<object, ValidationResult> validate,
        Func<object, CancellationToken, Task<ValidationResult>> validateAsync)
    {
        Name = name;
        Type = type;
        Properties = properties;
        _propertiesByName = properties.ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        _createInstance = createInstance;
        _jsonProperties = jsonProperties;
        _validate = validate;
        _validateAsync = validateAsync;
        _defaultValues = Serialize(createInstance());
    }

    /// <summary>The name the setting is registered, stored and addressed under.</summary>
    public string Name { get; }

    /// <summary>The setting class.</summary>
    public Type Type { get; }

    /// <summary>The setting's values, in declaration order, under their camelCase JSON names.</summary>
    public IReadOnlyList<SettingPropertyDefinition> Properties { get; }

    /// <summary>
    /// The configuration section whose values stand in for the code default when the deployment
    /// supplies it, or <see langword="null"/> when the setting names none.
    /// </summary>
    public string? ConfigurationSection { get; internal set; }

    /// <summary>
    /// Whether the default is the configured one - the section was present when the catalogue was
    /// composed - rather than the code default.
    /// </summary>
    public bool HasConfiguredDefault { get; private set; }

    /// <summary>
    /// Closes the definition over <typeparamref name="T"/>: reads its read-write properties through
    /// System.Text.Json's own metadata - so a <c>[JsonIgnore]</c> or <c>[JsonPropertyName]</c> is
    /// honoured exactly as serialization would honour it - and serializes <c>new T()</c> once as the
    /// code default every resolution starts from.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A <see cref="SecretSettingAttribute"/> marks a property that is not a string, or names a bound
    /// property the class does not expose as a setting property.
    /// </exception>
    public static SettingDefinition Create<T>(string name, IValidator<T> validator)
        where T : class, new()
    {
        var typeInfo = SettingJson.Options.GetTypeInfo(typeof(T));
        var readWrite = typeInfo.Properties.Where(property => property.Get is not null && property.Set is not null).ToList();

        // C# property name -> JSON name, so a secret's bound properties (named with nameof) are matched
        // exactly as serialization names them.
        var jsonNames = readWrite
            .Where(property => property.AttributeProvider is System.Reflection.MemberInfo)
            .ToDictionary(property => ((System.Reflection.MemberInfo)property.AttributeProvider!).Name, property => property.Name, StringComparer.Ordinal);

        var properties = new List<SettingPropertyDefinition>();
        foreach (var property in readWrite)
        {
            var secret = property.AttributeProvider?.GetCustomAttributes(typeof(SecretSettingAttribute), inherit: true)
                .OfType<SecretSettingAttribute>()
                .FirstOrDefault();
            if (secret is null)
            {
                properties.Add(new SettingPropertyDefinition(property.Name, property.PropertyType));
                continue;
            }

            if (property.PropertyType != typeof(string))
            {
                throw new InvalidOperationException(
                    $"The property '{property.Name}' of the setting '{name}' is marked [SecretSetting] but is of type '{property.PropertyType.Name}'. A secret must be a string.");
            }

            var boundTo = new List<string>();
            foreach (var bound in secret.BoundTo)
            {
                if (!jsonNames.TryGetValue(bound, out var jsonName) || jsonName == property.Name)
                {
                    throw new InvalidOperationException(
                        $"The secret '{property.Name}' of the setting '{name}' is bound to '{bound}', which is not another property of {typeof(T).Name}.");
                }

                boundTo.Add(jsonName);
            }

            properties.Add(new SettingPropertyDefinition(property.Name, property.PropertyType, IsSecret: true, BoundTo: boundTo));
        }

        return new SettingDefinition(
            name,
            typeof(T),
            properties,
            () => new T(),
            readWrite,
            instance => validator.Validate((T)instance),
            (instance, cancellationToken) => validator.ValidateAsync((T)instance, cancellationToken));
    }

    /// <summary>
    /// Replaces the code default with the values of <see cref="ConfigurationSection"/>, when the setting
    /// names one and <paramref name="configuration"/> supplies it: the section is bound onto a fresh
    /// <c>new T()</c>, so a key the section leaves out keeps its property initializer. A missing section
    /// leaves the code default in place.
    /// </summary>
    /// <param name="configuration">The application's configuration.</param>
    /// <returns>Whether the default is now the configured one.</returns>
    /// <exception cref="InvalidOperationException">A value in the section cannot be converted to its property's type.</exception>
    public bool ApplyConfiguredDefault(IConfiguration configuration)
    {
        if (ConfigurationSection is null)
        {
            return false;
        }

        var section = configuration.GetSection(ConfigurationSection);
        if (!section.Exists())
        {
            return false;
        }

        var instance = _createInstance();
        try
        {
            section.Bind(instance);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The configuration section '{ConfigurationSection}' cannot be bound to the setting '{Name}': {exception.Message}", exception);
        }

        _defaultValues = Serialize(instance);
        HasConfiguredDefault = true;
        return true;
    }

    /// <summary>A fresh copy of the default - configured, or else the code default - as a JSON object, one member per property.</summary>
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

    /// <summary>Serializes an instance's read-write properties into a JSON object, one member per property.</summary>
    private JsonObject Serialize(object instance)
    {
        var values = new JsonObject();
        foreach (var property in _jsonProperties)
        {
            values[property.Name] = JsonSerializer.SerializeToNode(property.Get!(instance), property.PropertyType, SettingJson.Options);
        }

        return values;
    }
}

/// <summary>
/// One value of a setting: its camelCase JSON name, the CLR type a stored or submitted value must
/// deserialize to, and whether it is a secret.
/// </summary>
/// <param name="Name">The property's JSON name.</param>
/// <param name="Type">The property's type.</param>
/// <param name="IsSecret">
/// Whether the property carries <see cref="SecretSettingAttribute"/> - stored encrypted and never
/// returned by the API.
/// </param>
/// <param name="BoundTo">
/// For a secret, the JSON names of the properties that decide where it is sent: it is never inherited
/// from a layer below one that overrides any of them (see <see cref="SecretSettingAttribute"/>).
/// </param>
sealed record SettingPropertyDefinition(string Name, Type Type, bool IsSecret = false, IReadOnlyList<string>? BoundTo = null)
{
    /// <summary>The JSON names of the properties this secret is bound to; empty for any other property.</summary>
    public IReadOnlyList<string> BoundProperties => BoundTo ?? [];

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
