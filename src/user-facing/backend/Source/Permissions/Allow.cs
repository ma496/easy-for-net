namespace Backend.Permissions;

// ReSharper disable InconsistentNaming

/// <summary>
/// Centralized catalog of permission name constants used to authorize
/// endpoint and service operations across the backend.
/// </summary>
public partial class Allow
{
    public const string Admin_Access = "Admin.Access";

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

    public const string Localization_View = "Localization.View";
    public const string Localization_Update = "Localization.Update";

    public const string Settings_View = "Settings.View";
    public const string Settings_Update = "Settings.Update";

    public const string Seo_View = "Seo.View";
    public const string Seo_Update = "Seo.Update";

    public const string Hangfire_Dashboard = "Hangfire.Dashboard";
}
