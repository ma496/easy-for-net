using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Seo.Core.Entities;
using Backend.Settings;

namespace Backend.ShareData;

/// <summary>
/// Populates the database with baseline data on first run and keeps the permission catalogue, the
/// administrator role and the administrator account in sync with the definitions declared in code.
/// </summary>
/// <remarks>
/// Every baseline row lives here rather than in a migration, and every step is written so that
/// running it again changes nothing: a project generated from this template starts with an empty
/// schema and no migration history at all, so its first start has to reach exactly the state this
/// repository's own database reaches.
/// </remarks>
public class DataSeeder(IUserService userService,
                        IRoleService roleService,
                        IPermissionService permissionService,
                        IPermissionDefinitionService permissionDefinitionService,
                        IOptions<SeedSetting> seedSetting,
                        AppDbContext dbContext)
{
    /// <summary>The system-created role that holds every permission the catalogue declares.</summary>
    public const string AdminRoleName = "Admin";
    private const string AdminRoleNameNormalized = "admin";

    /// <summary>
    /// The role an account created by sign-up is given by default (<c>General.SignupRoleName</c>). It is
    /// seeded with no permission at all and is an ordinary role, so an administrator may grant it some.
    /// </summary>
    public const string UserRoleName = "User";
    private const string UserRoleNameNormalized = "user";

    /// <summary>The seeded administrator account.</summary>
    public const string AdminUsername = "admin";
    private const string AdminEmail = "admin@example.com";

    /// <summary>
    /// Reconciles persisted permissions, the administrator role and account with the definitions
    /// declared in code, and inserts the default user role, sample notifications and SEO pages when
    /// none exist.
    /// </summary>
    public async Task SeedAsync()
    {
        var permissions = await ReconcilePermissionsAsync();
        var adminUser = await SeedAdminUserAsync();

        await ReconcileAdminRoleAsync(permissions, adminUser);
        await SeedUserRoleAsync();
        await SeedSampleNotificationsAsync(adminUser);
        await SeedSeoPagesAsync();
    }

    /// <summary>
    /// Brings the stored permission catalogue in line with the one declared in code - adding what is
    /// new, renaming what has changed, and deleting what is gone along with the role assignments of
    /// the deleted ones.
    /// </summary>
    /// <returns>Every permission the catalogue holds once it has been reconciled.</returns>
    private async Task<List<Permission>> ReconcilePermissionsAsync()
    {
        var flattenedPermissions = permissionDefinitionService.GetFlattenedPermissions();
        var savedPermissions = await permissionService.Permissions().AsNoTracking().ToListAsync();
        var permissionsToAdd = flattenedPermissions.Where(p => !savedPermissions.Any(sp => sp.Name == p.Name)).ToList();
        var permissionsToUpdate = flattenedPermissions
            .Where(p => savedPermissions.Any(sp => sp.Name == p.Name && sp.DisplayName != p.DisplayName))
            .ToList();
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
    /// Creates the administrator account when it is absent. The password is applied only on creation.
    /// </summary>
    /// <returns>The seeded account.</returns>
    private async Task<User> SeedAdminUserAsync()
    {
        var account = await userService.GetByUsernameAsync(AdminUsername);
        if (account is not null)
        {
            return account;
        }

        return await userService.CreateAsync(
            new User { SystemCreated = true, Username = AdminUsername, Email = AdminEmail, IsEmailVerified = true },
            seedSetting.Value.AdminPassword);
    }

    /// <summary>
    /// Keeps the system-created administrator role holding every permission the catalogue declares, and
    /// keeps the seeded administrator account in it.
    /// </summary>
    /// <param name="permissions">Every permission the catalogue holds.</param>
    /// <param name="adminUser">The seeded administrator account.</param>
    private async Task ReconcileAdminRoleAsync(List<Permission> permissions, User adminUser)
    {
        var adminRoleId = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.NameNormalized == AdminRoleNameNormalized)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync()
            ?? (await roleService.CreateAsync(new Role
            {
                SystemCreated = true,
                Name = AdminRoleName,
                Description = "Administrator Role"
            })).Id;

        await ReconcileRolePermissionsAsync(adminRoleId, permissions);

        if (!await userService.IsInRoleAsync(adminUser.Id, adminRoleId))
        {
            await userService.AssignRoleAsync(adminUser.Id, adminRoleId);
        }
    }

    /// <summary>
    /// Creates the default role of signed-up accounts once, with no permissions. A role of that name
    /// that was deleted is not brought back: its name stays reserved, and an administrator who deleted
    /// it meant to.
    /// </summary>
    private async Task SeedUserRoleAsync()
    {
        var exists = await dbContext.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(r => r.NameNormalized == UserRoleNameNormalized);
        if (exists)
        {
            return;
        }

        await roleService.CreateAsync(new Role
        {
            Name = UserRoleName,
            Description = "Default role of signed-up accounts"
        });
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
    /// Inserts the sample notifications when none exist: a few addressed to the administrator, and a
    /// welcome broadcast addressed to every user.
    /// </summary>
    /// <param name="adminUser">The seeded administrator account.</param>
    private async Task SeedSampleNotificationsAsync(User adminUser)
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

    /// <summary>
    /// Inserts the starter SEO pages - the home, about and contact pages, for every language - each only
    /// when no page exists for its path yet, deleted ones included, so a page an administrator removed
    /// is not brought back.
    /// </summary>
    private async Task SeedSeoPagesAsync()
    {
        (string Path, string Title, string Description)[] pages =
        [
            ("/", "Home", "Welcome to our site."),
            ("/about", "About", "Learn who we are and what we do."),
            ("/contact", "Contact", "Get in touch with us."),
        ];

        var paths = pages.Select(page => page.Path).ToList();
        var existing = await dbContext.SeoPages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(page => paths.Contains(page.Path))
            .Select(page => page.Path)
            .ToListAsync();

        var missing = pages.Where(page => !existing.Contains(page.Path)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        dbContext.SeoPages.AddRange(missing.Select(page => new SeoPage
        {
            Path = page.Path,
            Culture = null,
            Title = page.Title,
            Description = page.Description
        }));
        await dbContext.SaveChangesAsync();
    }
}
