using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CSharpTemplate.Common.Identity.Permissions;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PermissionAuthorizationHandler(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }
    
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, 
        PermissionRequirement requirement)
    {
        var userId = context.User.Identities
            .FirstOrDefault()?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
        
        if (!long.TryParse(userId, out long parseUserId))
            return;

        using var scope = _serviceScopeFactory.CreateScope();

        var permissionManager = scope.ServiceProvider.GetRequiredService<IPermissionManager>();
        var permissions = await permissionManager.GetPermissions(parseUserId);
        
        if (permissions.Permissions.Contains(requirement.Permission))
            context.Succeed(requirement);
    }
}