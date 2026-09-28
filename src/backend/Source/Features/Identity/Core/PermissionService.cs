namespace Backend.Features.Identity.Core;

using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Defines CRUD and lookup operations for <see cref="Permission"/> entities, including the
/// role/permission junction.
/// </summary>
public interface IPermissionService
{
    Task<Permission?> GetByIdAsync(Guid id);
    Task<Permission?> GetByNameAsync(string name);
    IQueryable<Permission> Permissions();
    Task<Permission> CreateAsync(Permission permission);
    Task CreateAsync(List<Permission> permissions);
    Task UpdateAsync(Permission permission);
    Task DeleteAsync(Guid id);
    Task DeleteAsync(List<Guid> ids);
    Task DeleteAsync(Permission permission);
    Task<List<Permission>> GetRolePermissionsAsync(Guid roleId);
    Task RemovePermissionFromAllRoles(Guid permissionId);
    Task RemovePermissionsFromAllRoles(List<Guid> permissionIds);
}

/// <summary>
/// EF Core-backed implementation of <see cref="IPermissionService"/> that manages permission entities and their
/// links to roles and users.
/// </summary>
[NoDirectUse]
public class PermissionService(AppDbContext dbContext) : IPermissionService
{
    public async Task<Permission?> GetByIdAsync(Guid id)
    {
        return await dbContext.Permissions.FindAsync(id);
    }

    public async Task<Permission?> GetByNameAsync(string name)
    {
        return await dbContext.Permissions.FirstOrDefaultAsync(p => p.Name == name);
    }

    public IQueryable<Permission> Permissions()
    {
        return dbContext.Permissions;
    }

    public async Task<Permission> CreateAsync(Permission permission)
    {
        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync();
        return permission;
    }

    public async Task CreateAsync(List<Permission> permissions)
    {
        dbContext.Permissions.AddRange(permissions);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Permission permission)
    {
        dbContext.Permissions.Update(permission);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var permission = await GetByIdAsync(id);
        if (permission != null)
        {
            await DeleteAsync(permission);
        }
    }

    public async Task DeleteAsync(List<Guid> ids)
    {
        await dbContext.Permissions
            .Where(p => ids.Contains(p.Id))
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAsync(Permission permission)
    {
        dbContext.Permissions.Remove(permission);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<Permission>> GetRolePermissionsAsync(Guid roleId)
    {
        return await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .ToListAsync();
    }

    public async Task RemovePermissionFromAllRoles(Guid permissionId)
    {
        await dbContext.RolePermissions
            .Where(rp => rp.PermissionId == permissionId)
            .ExecuteDeleteAsync();
    }

    public async Task RemovePermissionsFromAllRoles(List<Guid> permissionIds)
    {
        await dbContext.RolePermissions
            .Where(rp => permissionIds.Contains(rp.PermissionId))
            .ExecuteDeleteAsync();
    }
} 