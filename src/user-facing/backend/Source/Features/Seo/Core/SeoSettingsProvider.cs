using Backend.Features.Settings.Core;

namespace Backend.Features.Seo.Core;

/// <summary>
/// Declares the <see cref="SeoSettings"/> the Seo slice owns.
/// </summary>
public class SeoSettingsProvider : ISettingDefinitionProvider
{
    /// <summary>The name <see cref="SeoSettings"/> is registered, stored and addressed under.</summary>
    public const string SeoSettingName = "Seo";

    public void Define(SettingDefinitionContext context)
    {
        context.Add<SeoSettings>(SeoSettingName, new SeoSettingsValidator());
    }
}
