using EasyForNet.Application.Dependencies;

namespace CSharpTemplate.Common.Identity.Permissions.Provider;

public abstract class PermissionsProvider : IPermissionsProvider, ITransientDependency
{
    public virtual void Permissions(IPermissionsContext context)
    {
        var identityGroup = context.CreateGroup("Identity", "Identity");
        
        var usersPermission = identityGroup.AddPermission(IdentityPermissions.Users, "Users");
        usersPermission.AddChild(IdentityPermissions.CreateUser, "Create");
        usersPermission.AddChild(IdentityPermissions.UpdateUser, "Update");
        usersPermission.AddChild(IdentityPermissions.DeleteUser, "Delete");
        
        var rolesPermission = identityGroup.AddPermission(IdentityPermissions.Roles, "Roles");
        rolesPermission.AddChild(IdentityPermissions.CreateRole, "Create");
        rolesPermission.AddChild(IdentityPermissions.UpdateRole, "Update");
        rolesPermission.AddChild(IdentityPermissions.DeleteRole, "Delete");
    }
}