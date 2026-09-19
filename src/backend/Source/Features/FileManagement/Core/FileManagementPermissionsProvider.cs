namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Defines permissions for destructive file-management operations.
/// </summary>
/// <remarks>
/// Exercisable in either scope: files belong to the tenant they were uploaded in, and a platform
/// account acting in no tenant administers the ones that belong to none.
/// </remarks>
public class FileManagementPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "File Management";

    public void Define(PermissionDefinitionContext context)
    {
        var filesPermission = context.AddPermission("Files", "Files", PermissionScope.Both);
        filesPermission.AddChild(Allow.File_Delete, "Delete");
    }
}
