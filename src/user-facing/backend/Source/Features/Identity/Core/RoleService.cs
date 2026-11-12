using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Features.Identity.Core;

/// <summary>
/// Defines CRUD and lookup operations for <see cref="Role"/> entities, including management of role-permission assignments.
/// </summary>
/// <remarks>
/// Deletion is soft: the row is retained, so a deleted role's name stays reserved and the database's
/// uniqueness constraint keeps refusing it.
/// </remarks>
public interface IRoleService
{
    /// <summary>
    /// The role with this identifier, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="id">The identifier of the role being read.</param>
    /// <returns>The role, or <see langword="null"/> when there is none.</returns>
    Task<Role?> GetByIdAsync(Guid id);

    /// <summary>
    /// The role of this name, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="name">The name of the role being read.</param>
    /// <returns>The role, or <see langword="null"/> when there is none.</returns>
    Task<Role?> GetByNameAsync(string name);

    /// <summary>
    /// Every role that is not deleted. Lists, searches and counts all narrow from this one query.
    /// </summary>
    /// <returns>A composable query over the roles.</returns>
    IQueryable<Role> Roles();

    /// <summary>
    /// Creates a role.
    /// </summary>
    /// <param name="role">The role to create.</param>
    /// <returns>The created role.</returns>
    Task<Role> CreateAsync(Role role);

    Task UpdateAsync(Role role);

    /// <summary>
    /// Deletes the role with this identifier, and does nothing when there is none.
    /// </summary>
    /// <param name="id">The identifier of the role being deleted.</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Soft-deletes a role: the row is retained and stops being read anywhere, so the role grants
    /// nothing from the next request on while its name stays reserved and cannot be
    /// taken again by a role created later.
    /// </summary>
    /// <param name="role">The role being deleted.</param>
    Task DeleteAsync(Role role);

    Task AssignPermissionAsync(Guid roleId, Guid permissionId);
    Task AssignPermissionsAsync(Guid roleId, List<Guid> permissionIds);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId);
    Task RemovePermissionsAsync(Guid roleId, List<Guid> permissionIds);
    Task<List<string>> GetRolePermissionsAsync(Guid roleId);
}

/// <summary>
/// EF Core-backed implementation of <see cref="IRoleService"/> that manages roles and the role-permission junction table.
/// </summary>
[NoDirectUse]
public class RoleService(AppDbContext dbContext) : IRoleService
{
    /// <inheritdoc />
    public async Task<Role?> GetByIdAsync(Guid id)
    {
        return await Roles().FirstOrDefaultAsync(role => role.Id == id);
    }

    /// <inheritdoc />
    public async Task<Role?> GetByNameAsync(string name)
    {
        return await Roles().FirstOrDefaultAsync(role => role.Name == name);
    }

    /// <inheritdoc />
    public IQueryable<Role> Roles()
    {
        return dbContext.Roles;
    }

    /// <inheritdoc />
    public async Task<Role> CreateAsync(Role role)
    {
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();
        return role;
    }

    public async Task UpdateAsync(Role role)
    {
        dbContext.Roles.Update(role);
        await dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var role = await GetByIdAsync(id);
        if (role != null)
        {
            await DeleteAsync(role);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Role role)
    {
        // Removing a soft-deletable role marks it deleted instead of erasing it, so the row survives
        // to go on reserving its name under the uniqueness constraint while every read of it
        // - lists, lookups, and the role and permission sets a session is rebuilt from - stops
        // returning it.
        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync();
    }

    public async Task AssignPermissionAsync(Guid roleId, Guid permissionId)
    {
        var rolePermission = new RolePermission { RoleId = roleId, PermissionId = permissionId };
        dbContext.RolePermissions.Add(rolePermission);
        await dbContext.SaveChangesAsync();
    }

    public async Task AssignPermissionsAsync(Guid roleId, List<Guid> permissionIds)
    {
        var rolePermissions = permissionIds
            .Select(permissionId =>
                new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId
                });
        dbContext.RolePermissions.AddRange(rolePermissions);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemovePermissionAsync(Guid roleId, Guid permissionId)
    {
        var rolePermission = await dbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        if (rolePermission != null)
        {
            dbContext.RolePermissions.Remove(rolePermission);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task RemovePermissionsAsync(Guid roleId, List<Guid> permissionIds)
    {
        var rolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId && permissionIds.Contains(rp.PermissionId))
            .ToListAsync();
        dbContext.RolePermissions.RemoveRange(rolePermissions);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<string>> GetRolePermissionsAsync(Guid roleId)
    {
        return await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Name)
            .ToListAsync();
    }
}