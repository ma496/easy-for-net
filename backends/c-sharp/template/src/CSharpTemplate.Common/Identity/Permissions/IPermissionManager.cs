using CSharpTemplate.Common.Identity.Dto;

namespace CSharpTemplate.Common.Identity.Permissions;

public interface IPermissionManager
{
    Task SetPermissions(long userId);
    Task<PermissionsCacheModel> GetPermissions(long userId);
}