using System.Globalization;
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
        var savedPermissions = await _dbContext.Permissions.ToListAsync();

        var newPermissions = new List<Permission>();
        foreach (var p in permissions)
        {
            if(!savedPermissions.Exists(sp => sp.Name == p))
                newPermissions.Add(new Permission
                {
                    Name = p
                });
        }
        
        await _dbContext.Permissions.AddRangeAsync(newPermissions);

        var obsoletePermissions = new List<Permission>();
        foreach (var sp in savedPermissions)
        {
           if (!permissions.Contains(sp.Name))
               obsoletePermissions.Add(sp);
        }
        
        _dbContext.Permissions.RemoveRange(obsoletePermissions);
        
        await _dbContext.SaveChangesAsync();
    }

    private async Task CreateRoles()
    {
        var allPermissions = await _dbContext.Permissions.ToListAsync();
        
        await CreateRole("Developer", allPermissions);

        var permissions = allPermissions.Where(p => !p.Name.Contains(".Developer.")).ToList();
        
        await CreateRole("Admin", permissions);

        await _dbContext.SaveChangesAsync();
    }
    
    private async Task CreateRole(string roleName, List<Permission> permissions)
    {
        var savedRole = await _dbContext.Roles
            .Include(r => r.RolePermissions)
            .SingleOrDefaultAsync(r => r.Name == roleName);
        
        if (savedRole == null)
        {
            var role = new Role
            {
                Name = roleName,
                RolePermissions = permissions.Select(p => new RolePermission
                {
                    Permission = p
                }).ToList()
            };
            
            await _dbContext.Roles.AddAsync(role);
        }
        else
        {
            var newRolePermissions = new List<RolePermission>();
            foreach (var p in permissions)
            {
                if (!savedRole.RolePermissions.ToList().Exists(rp => rp.PermissionId == p.Id))
                    newRolePermissions.Add(new RolePermission
                    {
                        Role = savedRole,
                        Permission = p
                    });
            }

            await _dbContext.RolePermissions.AddRangeAsync(newRolePermissions);
        }
    }

    private async Task CreateUsers()
    {
        await CreateUser("developer", "developer", "Dev@3app");
        await CreateUser("admin", "admin", "admin123");

        await _dbContext.SaveChangesAsync();
    }
    
    private async Task CreateUser(string userName, string roleName, string password)
    {
        var role = await _dbContext.Roles.SingleAsync(r => r.Name.ToLower() == roleName);
        var savedUser = await _dbContext.Users.SingleOrDefaultAsync(u => u.Username == userName);

        if (savedUser == null)
        {
            var user = new User
            {
                Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(userName),
                Username = userName,
                Email = $"{userName}@{userName}.com",
                UserRoles = new List<UserRole>
                {
                    new()
                    {
                        Role = role
                    }
                }
            };
            await _dbContext.Users.AddAsync(user);
            await _userManager.UpdatePasswordAsync(user, password);
        }
    }

    #endregion
}