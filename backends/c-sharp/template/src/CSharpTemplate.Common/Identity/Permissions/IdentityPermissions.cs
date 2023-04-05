namespace CSharpTemplate.Common.Identity.Permissions;

public static class IdentityPermissions
{
    private const string Prefix = "Permissions.Identity";

    public const string Users = $"{Prefix}.Users";
    public const string CreateUser = $"{Users}.Create";
    public const string UpdateUser = $"{Users}.Update";
    public const string DeleteUser = $"{Users}.Delete";
    
    public const string Roles = $"{Prefix}.Roles";
    public const string CreateRole = $"{Roles}.Create";
    public const string UpdateRole = $"{Roles}.Update";
    public const string DeleteRole = $"{Roles}.Delete";
}