using CSharpTemplate.Common.Identity;
using CSharpTemplate.Common.Identity.Entities;
using CSharpTemplate.Common.Identity.Permissions.Provider;
using CSharpTemplate.Data.Context;
using EasyForNet.Data;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.App;

public class CSharpTemplateAppDataSeeder : DataSeeder
{
    private readonly CSharpTemplateDbContext _dbContext;
    private readonly IPermissionsContext _permissionsContext;
    private readonly IUserManager _userManager;

    public CSharpTemplateAppDataSeeder(CSharpTemplateDbContext dbContext, IPermissionsContext permissionsContext,
        IUserManager userManager)
    {
        _dbContext = dbContext;
        _permissionsContext = permissionsContext;
        _userManager = userManager;
    }
    
    public override async Task SeedAsync()
    {
        await CreatePermissions();
        await CreateRoles();
        await CreateUsers();
    }

    #region Utilities

    private async Task CreatePermissions()
    {
        var permissions = _permissionsContext.GetFlatAllPermissions();
        var entities = permissions.Select(p => new Permission
        {
            Name = p
        }).ToList();
        
        await _dbContext.Permissions.AddRangeAsync(entities);
        
        await _dbContext.SaveChangesAsync();
    }

    private async Task CreateRoles()
    {
        var allPermissions = await _dbContext.Permissions.ToListAsync();
        
        await CreateDeveloperRole(allPermissions);

        var permissions = allPermissions.Where(p => !p.Name.Contains(".Developer.")).ToList();
        
        await CreateAdminRole(permissions);

        await _dbContext.SaveChangesAsync();
    }
    
    private async Task CreateDeveloperRole(List<Permission> permissions)
    {
        var developerRole = new Role
        {
            Name = "Developer",
            RolePermissions = permissions.Select(p => new RolePermission
            {
                Permission = p
            }).ToList()
        };
        await _dbContext.Roles.AddAsync(developerRole);
    }

    private async Task CreateAdminRole(List<Permission> permissions)
    {
        var adminRole = new Role
        {
            Name = "Admin",
            RolePermissions = permissions.Select(p => new RolePermission
            {
                Permission = p
            }).ToList()
        };
        await _dbContext.Roles.AddAsync(adminRole);
    }

    private async Task CreateUsers()
    {
        await CreateDeveloperUser();
        await CreateAdminUser();

        await _dbContext.SaveChangesAsync();
    }
    
    private async Task CreateDeveloperUser()
    {
        var developerRole = await _dbContext.Roles.SingleAsync(r => r.Name.ToLower() == "developer");
        var developerUser = new User
        {
            Name = "Developer",
            Username = "developer",
            Email = "developer@developer.com",
            UserRoles = new List<UserRole>
            {
                new()
                {
                    Role = developerRole
                }
            }
        };
        await _dbContext.Users.AddAsync(developerUser);
        await _userManager.UpdatePasswordAsync(developerUser, "Dev#4app");
    }
    
    private async Task CreateAdminUser()
    {
        var adminRole = await _dbContext.Roles.SingleAsync(r => r.Name.ToLower() == "admin");
        var adminUser = new User
        {
            Name = "Admin",
            Username = "admin",
            Email = "admin@admin.com",
            UserRoles = new List<UserRole>
            {
                new()
                {
                    Role = adminRole
                }
            }
        };
        await _dbContext.Users.AddAsync(adminUser);
        await _userManager.UpdatePasswordAsync(adminUser, "admin123");
    }

    #endregion
}