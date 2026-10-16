using Backend.Features.Settings.Core;

namespace Backend.Features.Identity.Core;

/// <summary>
/// Declares the settings the Identity feature owns.
/// </summary>
public class IdentitySettingsProvider : ISettingDefinitionProvider
{
    /// <summary>The name <see cref="SigninSettings"/> is registered, stored and addressed under.</summary>
    public const string SigninSettingName = "Signin";

    public void Define(SettingDefinitionContext context)
    {
        context.Add<SigninSettings>(SigninSettingName, new SigninSettingsValidator());
    }
}