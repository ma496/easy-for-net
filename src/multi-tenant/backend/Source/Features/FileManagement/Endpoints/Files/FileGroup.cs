namespace Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// This route group that mounts file management endpoints under the
/// <c>file-management</c> URL prefix.
/// </summary>
public class FileGroup : Group
{
    public FileGroup()
    {
        Configure("file-management", ep => {});
    }
}