namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Defines permissions for destructive file-management operations.
/// </summary>
public class FileManagementPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "File Management";

    public void Define(PermissionDefinitionContext context)
    {
        var filesPermission = context.AddPermission("Files", "Files");
        filesPermission.AddChild(Allow.File_Delete, "Delete");
    }
}
