namespace Backend.Data;

using Backend.Data.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// Populates the database with baseline data on first run and keeps the permission catalogue, the
/// bootstrap tenant, the platform administrator role, the bootstrap tenant's administrator role, the
/// admin account and that account's membership in sync with the definitions declared in code.
/// </summary>
/// <remarks>
/// Every baseline row lives here rather than in a migration, and every step is written so that
/// running it again changes nothing: a project generated from this template starts with an empty
/// schema and no migration history at all, so its first start has to reach exactly the state this
/// repository's own database reaches. The seeder also runs before any request, so no tenant is
/// established when it starts: reads that span tenants say so with <c>AcrossAllTenants()</c>, and
/// every tenant-scoped write happens inside the scope it belongs to.
/// </remarks>
public class DataSeeder(IUserService userService,
                        IRoleService roleService,
                        IPermissionService permissionService,
                        IPermissionDefinitionService permissionDefinitionService,
                        ITenantContext tenantContext,
                        AppDbContext dbContext)
{
    /// <summary>
    /// Name carried by both administrator roles below. A role name is unique within its tenant, so the
    /// platform role and the bootstrap tenant's role share it without colliding, and each is found by
    /// its scope rather than by its name alone.
    /// </summary>
    private const string AdminRoleName = "Admin";
    private const string AdminRoleNameNormalized = "admin";
    private const string PlatformAdminRoleDescription = "Admin Role";
    private const string TenantAdminRoleDescription = "Tenant Admin Role";

    /// <summary>
    /// Name of the role earlier versions granted to self-service sign-ups. It belongs to no tenant and
    /// is no platform role either, so it is removed rather than reconciled.
    /// </summary>
    private const string PublicRoleNameNormalized = "public";

    private const string AdminUsername = "admin";
    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "Admin#123";

    /// <summary>
    /// Reconciles persisted permissions, tenants, roles, users and memberships with the definitions
    /// declared in code and inserts sample notification data for the admin user when none exists.
    /// </summary>
    public async Task SeedAsync()
    {
        await SeedBootstrapTenantAsync();

        var permissions = await ReconcilePermissionsAsync();
        var adminUser = await SeedAdminUserAsync();

        await ReconcilePlatformAdminRoleAsync(permissions, adminUser);
        await ReconcileBootstrapTenantAdministrationAsync(permissions, adminUser);
        await RemovePublicRoleAsync();
        await SeedSampleNotificationsAsync(adminUser);
    }

    /// <summary>
    /// Creates the system-created bootstrap tenant when the database does not have it yet. Its
    /// identity is fixed, so the row is recognised rather than duplicated on every later start, and
    /// the migration that inserts it into an existing database names that same identity.
    /// </summary>
    private async Task SeedBootstrapTenantAsync()
    {
        // A tenant is the scope itself rather than something inside one, so this read has no tenant
        // restriction to relax; naming the filter anyway records that the seeder runs with no tenant
        // established, and is a no-op on a kind that carries no tenant filter.
        var bootstrapTenantExists = await dbContext.Tenants
            .AcrossAllTenants()
            .AnyAsync(t => t.Id == TenancyConstants.BootstrapTenantId);
        if (bootstrapTenantExists)
        {
            return;
        }

        dbContext.Tenants.Add(new Tenant
        {
            Id = TenancyConstants.BootstrapTenantId,
            SystemCreated = true,
            Name = TenancyConstants.BootstrapTenantName,
            Identifier = TenancyConstants.BootstrapTenantIdentifier,
            Status = TenantStatus.Active
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Brings the stored permission catalogue in line with the one declared in code - adding what is
    /// new, renaming what has changed, and deleting what is gone along with the role assignments of
    /// the deleted ones. The catalogue is global and identical for every tenant, and this pass touches
    /// permissions and role-permission links alone: it deletes no tenant, no membership and no role.
    /// </summary>
    /// <returns>Every permission the catalogue holds once it has been reconciled.</returns>
    private async Task<List<Permission>> ReconcilePermissionsAsync()
    {
        var flattenedPermissions = permissionDefinitionService.GetFlattenedPermissions();
        var savedPermissions = await permissionService.Permissions().AsNoTracking().ToListAsync();
        var permissionsToAdd = flattenedPermissions.Where(p => !savedPermissions.Any(sp => sp.Name == p.Name)).ToList();
        var permissionsToUpdate = flattenedPermissions.Where(p => savedPermissions.Any(sp => sp.Name == p.Name && sp.DisplayName != p.DisplayName)).ToList();
        var permissionsToDelete = savedPermissions.Where(sp => flattenedPermissions.All(p => p.Name != sp.Name)).ToList();

        await permissionService.CreateAsync([.. permissionsToAdd.Select(p => new Permission { Name = p.Name, DisplayName = p.DisplayName })]);
        foreach (var permission in permissionsToUpdate)
        {
            var savedPermission = savedPermissions.FirstOrDefault(sp => sp.Name == permission.Name);
            if (savedPermission != null)
            {
                savedPermission.DisplayName = permission.DisplayName;
                await permissionService.UpdateAsync(savedPermission);
            }
        }
        await permissionService.RemovePermissionsFromAllRoles([.. permissionsToDelete.Select(p => p.Id)]);
        await permissionService.DeleteAsync([.. permissionsToDelete.Select(p => p.Id)]);

        return await permissionService.Permissions().AsNoTracking().ToListAsync();
    }

    /// <summary>
    /// Creates the seeded administrator account when it is absent. The account itself belongs to no
    /// tenant - one account is one identity across every tenant - and what places it inside the
    /// bootstrap tenant is the membership row written further down.
    /// </summary>
    /// <returns>The seeded administrator account.</returns>
    private async Task<User> SeedAdminUserAsync()
    {
        return await userService.GetByUsernameAsync(AdminUsername) ??
            await userService.CreateAsync(new User { SystemCreated = true, Username = AdminUsername, Email = AdminEmail, IsEmailVerified = true }, AdminPassword);
    }

    /// <summary>
    /// Keeps the platform administrator role holding the whole catalogue, the platform-tier
    /// permissions included, and keeps the seeded administrator account in it. The role names no
    /// tenant, which is what platform scope is, so administering the platform is a grant of its own
    /// that administering a tenant never implies.
    /// </summary>
    /// <param name="permissions">Every permission the catalogue holds.</param>
    /// <param name="adminUser">The seeded administrator account.</param>
    private async Task ReconcilePlatformAdminRoleAsync(List<Permission> permissions, User adminUser)
    {
        var platformAdminRole = await dbContext.Roles
            .AcrossAllTenants()
            .FirstOrDefaultAsync(r => r.TenantId == null && r.NameNormalized == AdminRoleNameNormalized);

        if (platformAdminRole is null)
        {
            // A row that names no tenant is only ever written in platform scope: with nothing
            // established the save refuses the write rather than storing an unattributed row.
            using (tenantContext.BeginPlatformScope())
            {
                platformAdminRole = await roleService.CreateAsync(new Role
                {
                    SystemCreated = true,
                    Name = AdminRoleName,
                    Description = PlatformAdminRoleDescription
                });
            }
        }

        await ReconcileRolePermissionsAsync(platformAdminRole.Id, permissions);
        await AssignRoleIfMissingAsync(adminUser.Id, platformAdminRole.Id);
    }

    /// <summary>
    /// Provisions the bootstrap tenant with its own system-created administrator role holding every
    /// tenant-tier permission, places the seeded administrator account in the tenant, and grants that
    /// first member the role - the same provisioning every tenant created later receives.
    /// </summary>
    /// <param name="permissions">Every permission the catalogue holds.</param>
    /// <param name="adminUser">The seeded administrator account.</param>
    private async Task ReconcileBootstrapTenantAdministrationAsync(List<Permission> permissions, User adminUser)
    {
        // A tenant role may hold no platform-tier permission, so the platform tier is subtracted here
        // rather than filtered out wherever the role is read.
        var platformPermissionNames = permissionDefinitionService.GetPlatformPermissionNames();
        var tenantPermissions = permissions.Where(p => !platformPermissionNames.Contains(p.Name)).ToList();

        using (tenantContext.BeginTenant(TenancyConstants.BootstrapTenantId))
        {
            // Inside the scope the tenant filter restricts the read to the bootstrap tenant and the
            // save attributes the new row to it, so neither has to name the tenant a second time.
            var tenantAdminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.NameNormalized == AdminRoleNameNormalized) ??
                await roleService.CreateAsync(new Role
                {
                    SystemCreated = true,
                    Name = AdminRoleName,
                    Description = TenantAdminRoleDescription
                });

            await ReconcileRolePermissionsAsync(tenantAdminRole.Id, tenantPermissions);

            if (!await dbContext.TenantMemberships.AnyAsync(m => m.UserId == adminUser.Id))
            {
                dbContext.TenantMemberships.Add(new TenantMembership { UserId = adminUser.Id });
                await dbContext.SaveChangesAsync();
            }

            await AssignRoleIfMissingAsync(adminUser.Id, tenantAdminRole.Id);
        }
    }

    /// <summary>
    /// Grants a role exactly the permissions it is meant to hold: assigns the ones it is missing and
    /// removes the ones that have left its set, such as a permission the catalogue no longer declares.
    /// </summary>
    /// <param name="roleId">The role being reconciled.</param>
    /// <param name="permissions">The permissions the role should hold, and no others.</param>
    private async Task ReconcileRolePermissionsAsync(Guid roleId, List<Permission> permissions)
    {
        var assignedPermissions = await permissionService.GetRolePermissionsAsync(roleId);

        var permissionsToAssign = permissions.Where(p => assignedPermissions.All(ap => ap.Name != p.Name)).ToList();
        await roleService.AssignPermissionsAsync(roleId, [.. permissionsToAssign.Select(p => p.Id)]);

        var permissionsToRemove = assignedPermissions.Where(ap => permissions.All(p => p.Name != ap.Name)).ToList();
        await roleService.RemovePermissionsAsync(roleId, [.. permissionsToRemove.Select(p => p.Id)]);
    }

    /// <summary>
    /// Assigns a role to an account unless the account already holds it.
    /// </summary>
    /// <param name="userId">The account being granted the role.</param>
    /// <param name="roleId">The role being granted.</param>
    private async Task AssignRoleIfMissingAsync(Guid userId, Guid roleId)
    {
        if (!await userService.IsInRoleAsync(userId, roleId))
        {
            await userService.AssignRoleAsync(userId, roleId);
        }
    }

    /// <summary>
    /// Removes the platform-scoped Public role and its assignments. Earlier versions seeded it for
    /// self-service sign-ups, so a database seeded by one still carries the row: it belongs to no
    /// tenant and is no platform role either, and leaving it would leave a role with no scope behind.
    /// A role of the same name created inside a tenant is that tenant's own and is left alone.
    /// </summary>
    private async Task RemovePublicRoleAsync()
    {
        var publicRoleIds = await dbContext.Roles
            .AcrossAllTenants()
            .Where(r => r.TenantId == null && r.NameNormalized == PublicRoleNameNormalized)
            .Select(r => r.Id)
            .ToListAsync();

        if (publicRoleIds.Count == 0)
        {
            return;
        }

        // The assignments go first and the role last, so nothing is ever left pointing at a role that
        // is gone. These are deliberately hard deletes: soft-deleting the role would retain the very
        // row this is removing.
        await dbContext.UserRoles.Where(ur => publicRoleIds.Contains(ur.RoleId)).ExecuteDeleteAsync();
        await dbContext.RolePermissions.Where(rp => publicRoleIds.Contains(rp.RoleId)).ExecuteDeleteAsync();
        await dbContext.Roles.AcrossAllTenants().Where(r => publicRoleIds.Contains(r.Id)).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Inserts the sample notifications when none exist. The ones addressed to the seeded
    /// administrator belong to the bootstrap tenant, while the welcome notification addresses nobody in
    /// particular and belongs to no tenant, so it stays visible from inside every tenant.
    /// </summary>
    /// <param name="adminUser">The seeded administrator account.</param>
    private async Task SeedSampleNotificationsAsync(User adminUser)
    {
        using (tenantContext.BeginTenant(TenancyConstants.BootstrapTenantId))
        {
            if (!await dbContext.Notifications.AnyAsync(x => x.UserId == adminUser.Id))
            {
                dbContext.Notifications.AddRange(
                    new Notification
                    {
                        UserId = adminUser.Id,
                        Type = NotificationType.Warning,
                        TitleKey = "notifications.inventoryBelowLimit.title",
                        MessageKey = "notifications.inventoryBelowLimit.message",
                        IsRead = false,
                        Group = "inventory",
                        Metadata = "{\"itemName\":\"Widget A\",\"currentQty\":5,\"minQty\":10}"
                    },
                    new Notification
                    {
                        UserId = adminUser.Id,
                        Type = NotificationType.Info,
                        TitleKey = "notifications.systemUpdate.title",
                        MessageKey = "notifications.systemUpdate.message",
                        IsRead = false,
                        Group = "system",
                        Metadata = "{\"date\":\"2026-04-25\"}"
                    },
                    new Notification
                    {
                        UserId = adminUser.Id,
                        Type = NotificationType.Success,
                        TitleKey = "notifications.orderCompleted.title",
                        MessageKey = "notifications.orderCompleted.message",
                        IsRead = true,
                        Group = "orders",
                        Metadata = "{\"orderId\":\"ORD-12345\"}"
                    },
                    new Notification
                    {
                        UserId = adminUser.Id,
                        Type = NotificationType.Error,
                        TitleKey = "notifications.paymentFailed.title",
                        MessageKey = "notifications.paymentFailed.message",
                        IsRead = false,
                        Group = "payments",
                        Metadata = "{\"orderId\":\"ORD-12346\",\"amount\":199.99}"
                    });
                await dbContext.SaveChangesAsync();
            }
        }

        // Platform scope is resolved with no tenant, which is the only state that writes a row
        // belonging to no tenant and the only one whose reads see those rows.
        using (tenantContext.BeginPlatformScope())
        {
            if (!await dbContext.Notifications.AnyAsync(x => x.UserId == null))
            {
                dbContext.Notifications.Add(new Notification
                {
                    UserId = null,
                    Type = NotificationType.Info,
                    TitleKey = "notifications.welcome.title",
                    MessageKey = "notifications.welcome.message",
                    IsRead = false,
                    Group = "system"
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
