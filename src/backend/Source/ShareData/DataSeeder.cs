namespace Backend.ShareData;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;

/// <summary>
/// Populates the database with baseline data on first run and keeps the permission catalogue, the
/// bootstrap tenant, the platform administrator role, the bootstrap tenant's administrator role, the
/// platform administrator account, the bootstrap tenant's administrator account and that account's
/// membership in sync with the definitions declared in code.
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
                        IFeatureDefinitionService featureDefinitionService,
                        IFeatureValueStore featureValueStore,
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
    /// The platform account. It holds the platform role only and belongs to no tenant, so
    /// administering the platform never depends on a membership anywhere.
    /// </summary>
    private const string PlatformAdminUsername = "admin";
    private const string PlatformAdminEmail = "admin@example.com";
    private const string PlatformAdminPassword = "Admin#123";

    /// <summary>
    /// The bootstrap tenant's administrator account - an identity separate from the platform
    /// administrator, so the tenant is administered from inside it the way every later tenant is.
    /// </summary>
    private const string TenantAdminUsername = "tenantadmin";
    private const string TenantAdminEmail = "tenantadmin@example.com";
    private const string TenantAdminPassword = "Admin#123";

    /// <summary>
    /// Reconciles persisted permissions, tenants, roles, users and memberships with the definitions
    /// declared in code and inserts sample notification data for the bootstrap tenant's administrator
    /// when none exists.
    /// </summary>
    public async Task SeedAsync()
    {
        await SeedBootstrapTenantAsync();

        var permissions = await ReconcilePermissionsAsync();
        await PruneOrphanFeatureValuesAsync();
        var platformAdminUser = await SeedUserAsync(PlatformAdminUsername, PlatformAdminEmail, PlatformAdminPassword, isPlatform: true);
        var tenantAdminUser = await SeedUserAsync(TenantAdminUsername, TenantAdminEmail, TenantAdminPassword, isPlatform: false);

        await ReconcilePlatformAdminRoleAsync(permissions, platformAdminUser);
        await ReconcileBootstrapTenantAdministrationAsync(permissions, tenantAdminUser);
        await SeedSampleNotificationsAsync(tenantAdminUser);
    }

    /// <summary>
    /// Removes stored feature values whose feature the code no longer declares.
    /// </summary>
    /// <remarks>
    /// The mirror of the delete step in <see cref="ReconcilePermissionsAsync"/>, and needed for the
    /// same reason: the catalogue is code, the values are rows naming it by string, so a feature
    /// renamed or removed would otherwise leave a row nobody can see in any screen and nobody can
    /// clear.
    /// <para>
    /// Note what this does <em>not</em> do. No feature definition is ever written to the database -
    /// unlike a permission, which is persisted only because a role grant needs a foreign key to point
    /// at. And no permission or grant is touched on account of a feature: turning a feature off hides
    /// permissions from the sessions of the tenants it applies to, it does not revoke anything, which
    /// is what lets turning it back on restore them with nothing to re-grant.
    /// </para>
    /// </remarks>
    private async Task PruneOrphanFeatureValuesAsync()
    {
        await featureValueStore.PruneUnknownAsync(featureDefinitionService.GetNames());
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
        // The scope is reconciled beside the display name, because it is persisted only so that the
        // per-request narrowing can run in the database: the definitions stay the single source of it,
        // and this pass is what keeps the stored copy from ever disagreeing with them.
        var permissionsToUpdate = flattenedPermissions
            .Where(p => savedPermissions.Any(sp => sp.Name == p.Name && (sp.DisplayName != p.DisplayName || sp.Scope != p.Scope)))
            .ToList();
        var permissionsToDelete = savedPermissions.Where(sp => flattenedPermissions.All(p => p.Name != sp.Name)).ToList();

        await permissionService.CreateAsync([.. permissionsToAdd.Select(p => new Permission { Name = p.Name, DisplayName = p.DisplayName, Scope = p.Scope })]);
        foreach (var permission in permissionsToUpdate)
        {
            var savedPermission = savedPermissions.FirstOrDefault(sp => sp.Name == permission.Name);
            if (savedPermission != null)
            {
                savedPermission.DisplayName = permission.DisplayName;
                savedPermission.Scope = permission.Scope;
                await permissionService.UpdateAsync(savedPermission);
            }
        }
        await permissionService.RemovePermissionsFromAllRoles([.. permissionsToDelete.Select(p => p.Id)]);
        await permissionService.DeleteAsync([.. permissionsToDelete.Select(p => p.Id)]);

        return await permissionService.Permissions().AsNoTracking().ToListAsync();
    }

    /// <summary>
    /// Creates a seeded account when it is absent, and keeps its tier in line with what it is seeded
    /// to be. The account itself belongs to no tenant - one account is one identity across every
    /// tenant - so the tenant it works in, if any, is decided by a membership row written further down
    /// rather than by creating it.
    /// </summary>
    /// <param name="username">The account's username.</param>
    /// <param name="email">The account's email address.</param>
    /// <param name="password">The account's initial password.</param>
    /// <param name="isPlatform">Whether the account belongs to the platform tier.</param>
    /// <returns>The seeded account.</returns>
    /// <remarks>
    /// The tier is reconciled rather than only set on creation, so a database seeded before the tier
    /// existed - or one whose platform account was changed by hand - is corrected on the next start
    /// instead of quietly staying wrong.
    /// </remarks>
    private async Task<User> SeedUserAsync(string username, string email, string password, bool isPlatform)
    {
        var account = await userService.GetByUsernameAsync(username);
        if (account is null)
        {
            return await userService.CreateAsync(
                new User { SystemCreated = true, Username = username, Email = email, IsEmailVerified = true, IsPlatform = isPlatform },
                password);
        }

        if (account.IsPlatform != isPlatform)
        {
            account.IsPlatform = isPlatform;
            await userService.UpdateAsync(account);
        }

        return account;
    }

    /// <summary>
    /// Keeps the platform administrator role holding the whole catalogue, every scope included, and
    /// keeps the seeded platform account in it. The role names no tenant, which is what platform scope
    /// is, so administering the platform is a grant of its own that administering a tenant never
    /// implies. Holding the whole catalogue is also what gives a platform account the tenant's own
    /// authority when it enters one: the session narrows the role's permissions to the scope being
    /// acted in, so inside a tenant it exercises exactly the tenant tier.
    /// </summary>
    /// <param name="permissions">Every permission the catalogue holds.</param>
    /// <param name="platformAdminUser">The seeded platform administrator account.</param>
    private async Task ReconcilePlatformAdminRoleAsync(List<Permission> permissions, User platformAdminUser)
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
        await AssignRoleIfMissingAsync(platformAdminUser.Id, platformAdminRole.Id);
    }

    /// <summary>
    /// Provisions the bootstrap tenant with its own system-created administrator role holding every
    /// permission exercisable inside a tenant, places the seeded tenant administrator account in it, and grants
    /// that first member the role - the same provisioning every tenant created later receives.
    /// </summary>
    /// <param name="permissions">Every permission the catalogue holds.</param>
    /// <param name="tenantAdminUser">The seeded account that administers the bootstrap tenant.</param>
    private async Task ReconcileBootstrapTenantAdministrationAsync(List<Permission> permissions, User tenantAdminUser)
    {
        // A tenant role may hold only what can be exercised inside a tenant, so the set is narrowed
        // here rather than filtered out wherever the role is read.
        var tenantPermissionNames = permissionDefinitionService.GetPermissionNamesInScope(PermissionScope.Tenant);
        var tenantPermissions = permissions.Where(p => tenantPermissionNames.Contains(p.Name)).ToList();

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

            if (!await dbContext.TenantMemberships.AnyAsync(m => m.UserId == tenantAdminUser.Id))
            {
                dbContext.TenantMemberships.Add(new TenantMembership { UserId = tenantAdminUser.Id });
                await dbContext.SaveChangesAsync();
            }

            await AssignRoleIfMissingAsync(tenantAdminUser.Id, tenantAdminRole.Id);
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
    /// Inserts the sample notifications when none exist. The ones addressed to the bootstrap tenant's
    /// administrator belong to that tenant, while the welcome notification addresses nobody in
    /// particular and belongs to no tenant, so it stays visible from inside every tenant.
    /// </summary>
    /// <param name="tenantAdminUser">The seeded account that administers the bootstrap tenant.</param>
    private async Task SeedSampleNotificationsAsync(User tenantAdminUser)
    {
        using (tenantContext.BeginTenant(TenancyConstants.BootstrapTenantId))
        {
            if (!await dbContext.Notifications.AnyAsync(x => x.UserId == tenantAdminUser.Id))
            {
                dbContext.Notifications.AddRange(
                    new Notification
                    {
                        UserId = tenantAdminUser.Id,
                        Type = NotificationType.Warning,
                        TitleKey = "notifications.inventoryBelowLimit.title",
                        MessageKey = "notifications.inventoryBelowLimit.message",
                        IsRead = false,
                        Group = "inventory",
                        Metadata = "{\"itemName\":\"Widget A\",\"currentQty\":5,\"minQty\":10}"
                    },
                    new Notification
                    {
                        UserId = tenantAdminUser.Id,
                        Type = NotificationType.Info,
                        TitleKey = "notifications.systemUpdate.title",
                        MessageKey = "notifications.systemUpdate.message",
                        IsRead = false,
                        Group = "system",
                        Metadata = "{\"date\":\"2026-04-25\"}"
                    },
                    new Notification
                    {
                        UserId = tenantAdminUser.Id,
                        Type = NotificationType.Success,
                        TitleKey = "notifications.orderCompleted.title",
                        MessageKey = "notifications.orderCompleted.message",
                        IsRead = true,
                        Group = "orders",
                        Metadata = "{\"orderId\":\"ORD-12345\"}"
                    },
                    new Notification
                    {
                        UserId = tenantAdminUser.Id,
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
