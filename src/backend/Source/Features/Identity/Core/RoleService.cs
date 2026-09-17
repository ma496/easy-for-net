namespace Backend.Features.Identity.Core;

using Backend.Attributes;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Defines CRUD and lookup operations for <see cref="Role"/> entities, including management of role-permission assignments.
/// </summary>
/// <remarks>
/// A role belongs to at most one tenant, so every lookup here answers for the tenant being acted in
/// and for no other: a role of another tenant is simply not found, which is what makes reading,
/// updating, deleting and re-permissioning one behave exactly as they do for a role that never
/// existed. A caller holding platform administration is the single exception, and the widening that
/// grants it lives in <see cref="Roles"/> alone, so no list, search or count can disagree with the
/// lookups about which roles the caller may touch. Deletion is soft: the row is retained, so a
/// deleted role's name stays reserved within its tenant and the database's uniqueness constraint
/// keeps refusing it.
/// </remarks>
public interface IRoleService
{
    /// <summary>
    /// The role with this identifier that the caller may see, or <see langword="null"/> when there is
    /// none - which a role belonging to another tenant is, exactly as a role that does not exist is.
    /// </summary>
    /// <param name="id">The identifier of the role being read.</param>
    /// <returns>The role, or <see langword="null"/> when the caller may not see it.</returns>
    Task<Role?> GetByIdAsync(Guid id);

    /// <summary>
    /// The role of this name that the caller may see, or <see langword="null"/> when there is none.
    /// Names are unique within a tenant rather than across the installation, so this answers with the
    /// tenant's own role of that name. It narrows from the same set as every other read here, so for a
    /// caller holding platform administration it answers from every tenant's roles at once - where a
    /// name is no longer unique and any one tenant's role of that name may come back. Where it matters
    /// which tenant's role is meant, read it through <see cref="Roles"/> with a predicate that says so.
    /// </summary>
    /// <param name="name">The name of the role being read.</param>
    /// <returns>The role, or <see langword="null"/> when the caller may not see it.</returns>
    Task<Role?> GetByNameAsync(string name);

    /// <summary>
    /// The roles the caller may see and administer: the roles of the tenant being acted in, widened to
    /// every tenant's roles when the caller holds platform administration. Lists, searches and counts
    /// all narrow from this one query, so none of them can forget the restriction and none of them can
    /// disagree about it.
    /// </summary>
    /// <returns>A composable query over the roles the caller may see.</returns>
    /// <remarks>
    /// Deleted roles are excluded here as everywhere else - the soft-delete filter is untouched by the
    /// widening - so a deleted role is listed by nobody and grants nothing, while its row goes on
    /// reserving its name. Work with no scope established at all, such as a job that never named the
    /// tenant it acts for, fails with <see cref="TenantScopeNotEstablishedException"/> when the query
    /// runs, rather than quietly reading every tenant's roles or none - the widened set is the one
    /// exception, because relaxing tenant restriction leaves nothing left to establish a scope for.
    /// </remarks>
    IQueryable<Role> Roles();

    /// <summary>
    /// Creates a role, attributed to the tenant being acted in rather than to any tenant the caller
    /// named, so a role can only ever be created inside the tenant its creator is acting in.
    /// </summary>
    /// <param name="role">The role to create.</param>
    /// <returns>The created role.</returns>
    Task<Role> CreateAsync(Role role);

    Task UpdateAsync(Role role);

    /// <summary>
    /// Deletes the role with this identifier when the caller may see it, and does nothing at all when
    /// they may not - a role of another tenant is left untouched, exactly as a role that does not
    /// exist would be.
    /// </summary>
    /// <param name="id">The identifier of the role being deleted.</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Soft-deletes a role: the row is retained and stops being read anywhere, so the role grants
    /// nothing from the next request on while its name stays reserved within its tenant and cannot be
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
/// <remarks>
/// The tenant a role belongs to is never named in a predicate here: every read goes through the
/// <c>Tenant</c> query filter, which restricts the set to the tenant active at the moment the query
/// runs, and the one deliberate departure from it is the platform-administration widening in
/// <see cref="Roles"/>. The permission-assignment methods work on the junction table by role
/// identifier and do not re-check the tenant, because their caller has already established that the
/// role is one it may touch - the endpoints read the role through <see cref="Roles"/> first, and the
/// seeder works on roles it has just created inside the scope they belong to.
/// </remarks>
[NoDirectUse]
public class RoleService(AppDbContext dbContext, ICurrentUserService currentUserService, ITenantContext tenantContext) : IRoleService
{
    /// <inheritdoc />
    public async Task<Role?> GetByIdAsync(Guid id)
    {
        // Deliberately a query rather than FindAsync: Find answers from the change tracker first, so a
        // role another read had already loaded from outside the tenant would come back without the
        // tenant restriction ever being applied to it. Narrowing from Roles() instead is what makes a
        // role of another tenant read as missing, and what lets a platform administrator read any.
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
        // Platform administration is the tier test, and it is read from the request's live permission
        // claims, which the session check has already recomputed from current data. A holder of it
        // administers roles irrespective of the tenant those roles belong to, so tenant restriction is
        // relaxed by name and nothing else is: the soft-delete filter stays in force, so a deleted
        // role is no more visible to them than to anybody else. Acting in no tenant is the exception:
        // platform scope is about the platform's own roles, which the Tenant query filter already
        // narrows the set to there.
        if (currentUserService.HasPermission(Allow.Platform_Administration) && !IsPlatformScope())
        {
            return dbContext.Roles.AcrossAllTenants();
        }

        // Everybody else reads through the Tenant query filter, which restricts the set to the tenant
        // being acted in - and throws where no scope was established, rather than answering from every
        // tenant or from none.
        return dbContext.Roles;
    }

    /// <summary>
    /// Whether the request is running in platform scope - resolved, but acting in no tenant.
    /// </summary>
    private bool IsPlatformScope() => tenantContext is { IsResolved: true, CurrentTenantId: null };

    /// <inheritdoc />
    public async Task<Role> CreateAsync(Role role)
    {
        // The row takes its tenant from the active scope when it is saved, never from the caller, so
        // nothing here has to name a tenant and no payload can decide which tenant a role lands in.
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
        // The lookup is the tenant restriction: a role of another tenant is not found and therefore
        // not deleted, which is the same nothing that happens for an identifier naming no role at all.
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
        // to go on reserving its name under the tenant's uniqueness constraint while every read of it
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
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Name)
            .ToListAsync();
    }
}