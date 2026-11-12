namespace Backend.Permissions;

/// <summary>
/// Declares the permissions that guard the administration area as a whole rather than any one
/// feature: entering the admin area at all, and opening the Hangfire dashboard.
/// </summary>
/// <remarks>
/// <see cref="Allow.Admin_Access"/> is what separates an administrator from an ordinary signed-in
/// user: the web app opens its <c>/admin</c> area only to a session holding it, while every feature
/// screen inside that area still checks its own permission.
/// </remarks>
public class AdministrationPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Administration";

    public void Define(PermissionDefinitionContext context)
    {
        var adminPermissions = context.AddPermission("Admin", "Admin");
        adminPermissions.AddChild(Allow.Admin_Access, "Access");

        var hangfirePermissions = context.AddPermission("Hangfire", "Hangfire");
        hangfirePermissions.AddChild(Allow.Hangfire_Dashboard, "Dashboard");
    }
}
