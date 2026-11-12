namespace Backend.Features.Localization.Core;

/// <summary>
/// Declares the permission hierarchy for the Localization feature: viewing and editing translation
/// overrides and language settings.
/// </summary>
/// <remarks>
/// Exercisable in either scope: a platform account acting in no tenant edits the platform's own
/// overrides, and a tenant administrator edits their tenant's. Neither requires a feature - this is not
/// a plan-gated capability - and gating it would make the entitlement question circular, since the
/// screen these permissions guard is itself part of no plan.
/// </remarks>
public class LocalizationPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Localization";

    public void Define(PermissionDefinitionContext context)
    {
        var localizationPermissions = context.AddPermission("Localization", "Localization", PermissionScope.Both);
        localizationPermissions.AddChild(Allow.Localization_View, "View");
        localizationPermissions.AddChild(Allow.Localization_Update, "Update");
    }
}
