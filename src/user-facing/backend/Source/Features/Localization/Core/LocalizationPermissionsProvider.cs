namespace Backend.Features.Localization.Core;

/// <summary>
/// Declares the permission hierarchy for the Localization feature: viewing and editing translation
/// overrides and language settings.
/// </summary>
public class LocalizationPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Localization";

    public void Define(PermissionDefinitionContext context)
    {
        var localizationPermissions = context.AddPermission("Localization", "Localization");
        localizationPermissions.AddChild(Allow.Localization_View, "View");
        localizationPermissions.AddChild(Allow.Localization_Update, "Update");
    }
}
