using Microsoft.AspNetCore.Authorization;

namespace CSharpTemplate.Common.Identity.Permissions;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}