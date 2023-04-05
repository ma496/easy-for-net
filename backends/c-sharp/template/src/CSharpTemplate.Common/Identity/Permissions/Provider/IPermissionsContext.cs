namespace CSharpTemplate.Common.Identity.Permissions.Provider;

public interface IPermissionsContext
{
    PermissionsGroup CreateGroup(string name, string displayName);
    HashSet<PermissionsGroup> GetPermissionsByGroup();
    HashSet<PermissionsGroup> GetAllPermissionsByGroup();
}