namespace Backend.Permissions;

// ReSharper disable InconsistentNaming

/// <summary>
/// Centralized catalog of permission name constants used to authorize
/// endpoint and service operations across the backend.
/// </summary>
public partial class Allow
{
    public const string User_View = "User.View";
    public const string User_Create = "User.Create";
    public const string User_Update = "User.Update";
    public const string User_Delete = "User.Delete";

    public const string Role_View = "Role.View";
    public const string Role_Create = "Role.Create";
    public const string Role_Update = "Role.Update";
    public const string Role_Delete = "Role.Delete";
    public const string Role_ChangePermissions = "Role.ChangePermissions";

    public const string File_Delete = "File.Delete";

    public const string Tenant_View = "Tenant.View";
    public const string Tenant_Detail = "Tenant.Detail";
    public const string Tenant_Create = "Tenant.Create";
    public const string Tenant_Update = "Tenant.Update";
    public const string Tenant_Suspend = "Tenant.Suspend";
    public const string Tenant_Reactivate = "Tenant.Reactivate";
    public const string Tenant_Delete = "Tenant.Delete";

    public const string TenantMember_View = "TenantMember.View";
    public const string TenantMember_Add = "TenantMember.Add";
    public const string TenantMember_UpdateRoles = "TenantMember.UpdateRoles";
    public const string TenantMember_Remove = "TenantMember.Remove";

    public const string Edition_View = "Edition.View";
    public const string Edition_Create = "Edition.Create";
    public const string Edition_Update = "Edition.Update";
    public const string Edition_Delete = "Edition.Delete";

    public const string FeatureValue_View = "FeatureValue.View";
    public const string FeatureValue_Manage = "FeatureValue.Manage";
}
