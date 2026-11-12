namespace Backend.Features.Settings.Endpoints.Settings;

/// <summary>
/// This route group that prefixes every settings endpoint with the <c>settings</c> segment.
/// </summary>
sealed class SettingsGroup : Group
{
    public SettingsGroup()
    {
        Configure("settings", ep => {});
    }
}