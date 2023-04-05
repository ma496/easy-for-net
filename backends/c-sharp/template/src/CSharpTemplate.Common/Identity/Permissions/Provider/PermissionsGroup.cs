namespace CSharpTemplate.Common.Identity.Permissions.Provider;

public class PermissionsGroup
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    
    public HashSet<PermissionDefinition> Permissions { get; internal set; } = new();
    
    public override bool Equals(object? obj)
    {
        if (obj == null || !(obj is PermissionsGroup other))
            return false;

        return Name == other.Name;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
    
    public PermissionDefinition AddPermission(string name, string displayName)
    {
        var permissionDefinition = new PermissionDefinition
        {
            Name = name,
            DisplayName = displayName
        };
        
        Permissions.Add(permissionDefinition);
        
        return permissionDefinition;
    }
}