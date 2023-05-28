namespace CSharpTemplate.Common.Identity.Permissions.Provider;

public class PermissionsContext : IPermissionsContext
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
    
    public HashSet<string> GetFlatPermissions()
    {
        var groups = GetPermissionsByGroup();
        return GetFlatPermissions(groups);
    }
    
    public HashSet<string> GetFlatAllPermissions()
    {
        var groups = GetAllPermissionsByGroup();
        return GetFlatPermissions(groups);
    }

    #region Utilities

    private static HashSet<PermissionDefinition> RemoveDeveloperPermissions(HashSet<PermissionDefinition> permissions)
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

    private static HashSet<string> GetFlatPermissions(HashSet<PermissionsGroup> groups)
    {
        var result = new HashSet<string>();

        foreach (var group in groups)
        {
            result.UnionWith(GetFlatPermissions(group));
        }

        return result;
    }

    private static HashSet<string> GetFlatPermissions(PermissionsGroup group)
    {
        var result = new HashSet<string>();

        foreach (var permission in group.Permissions)
        {
            result.Add(permission.Name);

            result.UnionWith(GetFlatPermissions(permission));
        }

        return result;
    }

    private static HashSet<string> GetFlatPermissions(PermissionDefinition permission)
    {
        var result = new HashSet<string>();

        foreach (var childPermission in permission.Permissions)
        {
            result.Add(childPermission.Name);

            result.UnionWith(GetFlatPermissions(childPermission));
        }

        return result;
    }

    #endregion
}