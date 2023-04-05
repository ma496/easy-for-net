using EasyForNet.Application.Dependencies;

namespace CSharpTemplate.Common.Identity.Permissions.Provider;

public class PermissionsContext : IPermissionsContext, IScopedDependency
{
    private readonly HashSet<PermissionsGroup> _permissionsGroups = new();
    
    public PermissionsGroup CreateGroup(string name, string displayName)
    {
        var permissionsGroup = new PermissionsGroup
        {
            Name = name,
            DisplayName = displayName
        };

        _permissionsGroups.Add(permissionsGroup);
        
        return permissionsGroup;
    }

    public HashSet<PermissionsGroup> GetPermissionsByGroup()
    {
        var filteredPermissionsGroups = new HashSet<PermissionsGroup>();
        foreach (var permissionsGroup in _permissionsGroups)
        {
            var permissionsGroupCopy = new PermissionsGroup
            {
                Name = permissionsGroup.Name,
                DisplayName = permissionsGroup.DisplayName,
                Permissions = RemoveDeveloperPermissions(permissionsGroup.Permissions)
            };

            filteredPermissionsGroups.Add(permissionsGroupCopy);
        }

        return filteredPermissionsGroups;
    }
    
    public HashSet<PermissionsGroup> GetAllPermissionsByGroup()
    {
        return _permissionsGroups;
    }

    private HashSet<PermissionDefinition> RemoveDeveloperPermissions(HashSet<PermissionDefinition> permissions)
    {
        var filteredPermissions = new HashSet<PermissionDefinition>();
        foreach (var p in permissions)
        {
            var filteredPermission = new PermissionDefinition
            {
                Name = p.Name,
                DisplayName = p.DisplayName
            };
            if (p.Permissions.Count > 0)
                filteredPermission.Permissions = RemoveDeveloperPermissions(p.Permissions);
            if (!filteredPermission.Name.Contains(".Developer."))
                filteredPermissions.Add(filteredPermission);
        }

        return filteredPermissions;
    }
}