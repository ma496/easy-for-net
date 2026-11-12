namespace Backend.Features.Settings.Core;

/// <summary>
/// Declares the permission hierarchy for the Settings feature: viewing the settings the acting scope
/// resolves to, and overriding them.
/// </summary>
/// <remarks>
/// Exercisable in either scope: a platform account acting in no tenant edits the platform's overrides,
/// which every tenant inherits, and a tenant administrator edits their tenant's own. Neither requires a
/// feature - settings are not a plan-gated capability.
/// </remarks>
public class SettingsPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Settings";

    public void Define(PermissionDefinitionContext context)
    {
        var settingsPermissions = context.AddPermission("Settings", "Settings", PermissionScope.Both);
        settingsPermissions.AddChild(Allow.Settings_View, "View");
        settingsPermissions.AddChild(Allow.Settings_Update, "Update");
    }
}