namespace Backend.Features.Settings.Core;

/// <summary>
/// Returned by <see cref="SettingDefinitionContext.Add{T}"/> for the rest of one setting's definition.
/// </summary>
/// <remarks>
/// Options chain onto the registration: <c>context.Add&lt;T&gt;(...).FromConfiguration("Section")</c>.
/// The setting's default - the code default, or the configured default a section stands in with - is
/// held to the setting's validator when the catalogue is composed at startup. Properties holding
/// secrets are declared on the class itself, with <see cref="SecretSettingAttribute"/>.
/// </remarks>
/// <typeparam name="T">The setting class being defined.</typeparam>
[AllowOutside]
public sealed class SettingDefinitionBuilder<T>
    where T : class, new()
{
    internal SettingDefinitionBuilder(SettingDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>The definition this builder shapes.</summary>
    internal SettingDefinition Definition { get; }

    /// <summary>
    /// Lets the configuration section <paramref name="section"/> stand in for the code default: when the
    /// deployment supplies it, it is bound onto <c>new T()</c> - so a key it leaves out keeps its property
    /// initializer - and the result is the default every tenant and the platform start from. When the
    /// section is missing the code default stands. Read once, when the catalogue is composed at startup.
    /// </summary>
    /// <param name="section">The configuration section's key (<c>EmailSettings</c>, <c>Parent:Child</c>).</param>
    /// <returns>This builder, so further options chain.</returns>
    public SettingDefinitionBuilder<T> FromConfiguration(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        Definition.ConfigurationSection = section;
        return this;
    }
}
