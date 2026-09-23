namespace Backend.Features.Identity.Core;

using Backend.Permissions;

/// <summary>
/// Declares the permission hierarchy for the Identity feature (Users, Roles, and their CRUD sub-permissions).
/// </summary>
/// <remarks>
/// Both groups are exercisable in either scope: in platform scope they administer the platform's own
/// accounts and the roles belonging to no tenant, and inside a tenant they administer that tenant's.
/// <para>
/// Both groups are gated on the tenant's user-management feature, so a tenant whose plan does not
/// include it is never minted with any user or role permission and is not offered them on any role's
/// surface. Platform scope is inside no plan and is unaffected.
/// </para>
/// </remarks>
public class IdentityPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Identity";

    public void Define(PermissionDefinitionContext context)
    {
        var usersPermissions = context.AddPermission("Users", "Users", PermissionScope.Both)
                                      .RequireFeatures(FeatureNames.Identity_UserManagement);
        usersPermissions.AddChild(Allow.User_View, "View");
        usersPermissions.AddChild(Allow.User_Create, "Create");
        usersPermissions.AddChild(Allow.User_Update, "Update");
        usersPermissions.AddChild(Allow.User_Delete, "Delete");

        var rolesPermissions = context.AddPermission("Roles", "Roles", PermissionScope.Both)
                                      .RequireFeatures(FeatureNames.Identity_UserManagement);
        rolesPermissions.AddChild(Allow.Role_View, "View");
        rolesPermissions.AddChild(Allow.Role_Create, "Create");
        rolesPermissions.AddChild(Allow.Role_Update, "Update");
        rolesPermissions.AddChild(Allow.Role_Delete, "Delete");
        rolesPermissions.AddChild(Allow.Role_ChangePermissions, "Change Permissions");
    }
}
