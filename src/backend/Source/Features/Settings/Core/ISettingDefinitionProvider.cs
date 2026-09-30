namespace Backend.Features.Settings.Core;

/// <summary>
/// Implemented by a vertical slice to declare the settings it owns, in
/// <c>Core/&lt;X&gt;SettingsProvider.cs</c> beside its permission and entitlement providers.
/// </summary>
/// <remarks>
/// A setting is a C# class whose properties are its values, registered under a stable name with the
/// FluentValidation validator its values are held to: <c>context.Add&lt;SigninSettings&gt;("Signin", new SigninSettingsValidator())</c>.
/// Its code default is what its property initializers give (<c>new T()</c>), and that default is
/// validated when the application starts. Providers are discovered by reflection across the assembly,
/// so there is nothing to register by hand.
/// </remarks>
[AllowOutside]
public interface ISettingDefinitionProvider
{
    /// <summary>
    /// Adds the provider's settings to <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The context to register the settings with.</param>
    void Define(SettingDefinitionContext context);
}