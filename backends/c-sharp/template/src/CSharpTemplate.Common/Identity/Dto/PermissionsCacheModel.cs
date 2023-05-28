namespace CSharpTemplate.Common.Identity.Dto;

public class PermissionsCacheModel
{
    public HashSet<string> Permissions { get; set; } = new();
}