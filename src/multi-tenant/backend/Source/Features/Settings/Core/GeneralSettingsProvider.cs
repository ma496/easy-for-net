namespace Backend.Features.Settings.Core;

/// <summary>
/// Declares the <see cref="GeneralSettings"/> the Settings feature owns.
/// </summary>
public class GeneralSettingsProvider : ISettingDefinitionProvider
{
    /// <summary>The name <see cref="GeneralSettings"/> is registered, stored and addressed under.</summary>
    public const string GeneralSettingName = "General";

    public void Define(SettingDefinitionContext context)
    {
        context.Add<GeneralSettings>(GeneralSettingName, new GeneralSettingsValidator());
    }
}
