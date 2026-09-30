namespace Backend.Features.Settings.Core;

/// <summary>
/// Returned by <see cref="SettingDefinitionContext.Add{T}"/> for the rest of one setting's definition.
/// </summary>
/// <remarks>
/// It declares nothing yet. It exists so that options a setting may later need - a configuration
/// section standing in for its code default, properties that hold secrets - chain onto the
/// registration (<c>context.Add&lt;T&gt;(...).Option(...)</c>) without reshaping how providers register.
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
}