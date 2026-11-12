namespace Backend.Features.Settings.Core;

/// <summary>
/// Declares the permission hierarchy for the Settings feature: viewing the settings the application
/// resolves to, and overriding them.
/// </summary>
public class SettingsPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Settings";

    public void Define(PermissionDefinitionContext context)
    {
        var settingsPermissions = context.AddPermission("Settings", "Settings");
        settingsPermissions.AddChild(Allow.Settings_View, "View");
        settingsPermissions.AddChild(Allow.Settings_Update, "Update");
    }
}
