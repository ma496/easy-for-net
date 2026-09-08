namespace Backend.Data;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Permissions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Populates the database with baseline data on first run and keeps the
/// permission catalog, admin role, and admin user in sync with the current
/// code-defined definitions.
/// </summary>
public class DataSeeder(IUserService userService,
                        IRoleService roleService,
                        IPermissionService permissionService,
                        IPermissionDefinitionService permissionDefinitionService,
                        AppDbContext dbContext)
{
    /// <summary>
    /// Reconciles persisted permissions, roles, and users with the definitions
    /// declared in code and inserts sample notification data for the admin user
    /// when none exists.
    /// </summary>
    public async Task SeedAsync()
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

        // all permissions
        var permissions = await permissionService.Permissions().AsNoTracking().ToListAsync();

        // admin role
        var adminRole = await roleService.GetByNameAsync("Admin") ??
            await roleService.CreateAsync(new Role { SystemCreated = true, Name = "Admin", Description = "Admin Role" });
        var adminPermissions = await permissionService.GetRolePermissionsAsync(adminRole.Id);
        var adminPermissionsToAssign = permissions.Where(p => adminPermissions.All(ap => ap.Name != p.Name)).ToList();
        await roleService.AssignPermissionsAsync(adminRole.Id, [.. adminPermissionsToAssign.Select(p => p.Id)]);
        var adminPermissionsToRemove = adminPermissions.Where(ap => permissions.All(p => p.Name != ap.Name)).ToList();
        await roleService.RemovePermissionsAsync(adminRole.Id, [.. adminPermissionsToRemove.Select(p => p.Id)]);

        // admin user
        var adminUser = await userService.GetByUsernameAsync("admin") ??
            await userService.CreateAsync(new User { SystemCreated = true, Username = "admin", Email = "admin@example.com", IsEmailVerified = true }, "Admin#123");
        if (!await userService.IsInRoleAsync(adminUser.Id, adminRole.Id))
        {
            await userService.AssignRoleAsync(adminUser.Id, adminRole.Id);
        }

        // seed sample notifications for admin user
        var existingNotificationsCount = await dbContext.Notifications
            .CountAsync(x => x.UserId == adminUser.Id);
        if (existingNotificationsCount == 0)
        {
            var sampleNotifications = new List<Notification>
            {
                new()
                {
                    UserId = adminUser.Id,
                    Type = NotificationType.Warning,
                    TitleKey = "notifications.inventoryBelowLimit.title",
                    MessageKey = "notifications.inventoryBelowLimit.message",
                    IsRead = false,
                    Group = "inventory",
                    Metadata = "{\"itemName\":\"Widget A\",\"currentQty\":5,\"minQty\":10}"
                },
                new()
                {
                    UserId = adminUser.Id,
                    Type = NotificationType.Info,
                    TitleKey = "notifications.systemUpdate.title",
                    MessageKey = "notifications.systemUpdate.message",
                    IsRead = false,
                    Group = "system",
                    Metadata = "{\"date\":\"2026-04-25\"}"
                },
                new()
                {
                    UserId = adminUser.Id,
                    Type = NotificationType.Success,
                    TitleKey = "notifications.orderCompleted.title",
                    MessageKey = "notifications.orderCompleted.message",
                    IsRead = true,
                    Group = "orders",
                    Metadata = "{\"orderId\":\"ORD-12345\"}"
                },
                new()
                {
                    UserId = adminUser.Id,
                    Type = NotificationType.Error,
                    TitleKey = "notifications.paymentFailed.title",
                    MessageKey = "notifications.paymentFailed.message",
                    IsRead = false,
                    Group = "payments",
                    Metadata = "{\"orderId\":\"ORD-12346\",\"amount\":199.99}"
                },
                // Global notification (no user)
                new()
                {
                    UserId = null,
                    Type = NotificationType.Info,
                    TitleKey = "notifications.welcome.title",
                    MessageKey = "notifications.welcome.message",
                    IsRead = false,
                    Group = "system"
                }
            };

            dbContext.Notifications.AddRange(sampleNotifications);
            await dbContext.SaveChangesAsync();
        }

        // Public role
        var publicRole = await roleService.GetByNameAsync("Public") ??
            await roleService.CreateAsync(new Role { SystemCreated = true, Name = "Public", Description = "Public Role" });
    }
}
