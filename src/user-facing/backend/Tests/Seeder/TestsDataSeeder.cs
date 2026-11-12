using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Settings;
using Microsoft.Extensions.Options;

namespace Backend.Tests.Seeder;

/// <summary>
/// Seeds test data into the database - users, roles and their permissions - and populates the shared
/// <see cref="TestUsers"/> and <see cref="TestRoles"/> static holders with generated IDs.
/// </summary>
/// <remarks>
/// Idempotent: the shared fixture seeds once, but the seeder is written so that a second pass finds
/// what the first one made rather than duplicating it.
/// </remarks>
public class TestsDataSeeder(IUserService userService,
                             IRoleService roleService,
                             IPermissionService permissionService,
                             IOptions<SeedSetting> seedSetting,
                             AppDbContext dbContext)
{
    /// <summary>
    /// Seeds the test accounts and roles, capturing every generated ID on the static holders.
    /// </summary>
    public async Task SeedAsync()
    {
        TestUsers.SetAdminPassword(seedSetting.Value.AdminPassword);

        var permissions = await permissionService.Permissions().ToListAsync();

        var (testUserId, testRoleId) = await CreateUserWithRoleAsync(permissions, "test", "Test");
        var (testOneUserId, testOneRoleId) = await CreateUserWithRoleAsync(permissions, "testone", "TestOne");
        var (testTwoUserId, testTwoRoleId) = await CreateUserWithRoleAsync(permissions, "testtwo", "TestTwo");

        var limitedRoleId = await CreateLimitedRoleAsync(permissions);
        var limitedUserId = await CreateAccountAsync("limited", limitedRoleId);

        var adminRoleId = (await roleService.GetByNameAsync(DataSeeder.AdminRoleName))?.Id ?? Guid.Empty;
        var userRoleId = (await roleService.GetByNameAsync(DataSeeder.UserRoleName))?.Id ?? Guid.Empty;
        var plainUserId = await CreateAccountAsync("plain", userRoleId);

        await SeedStoredFilesForProfileImagesAsync();

        TestUsers.SetUserIds(
            adminUserId: (await userService.GetByUsernameAsync(TestUsers.AdminUsername))?.Id ?? Guid.Empty,
            testUserId,
            testOneUserId,
            testTwoUserId,
            limitedUserId,
            plainUserId);
        TestRoles.SetRoleIds(adminRoleId, userRoleId, testRoleId, testOneRoleId, testTwoRoleId, limitedRoleId);
    }

    /// <summary>
    /// Creates a role holding exactly one permission, so that a per-endpoint <c>Permissions(...)</c>
    /// declaration can be proved to be what turns a caller away.
    /// </summary>
    private async Task<Guid> CreateLimitedRoleAsync(List<Permission> permissions)
    {
        var role = await roleService.GetByNameAsync("Limited")
                   ?? await roleService.CreateAsync(new Role
                   {
                       SystemCreated = true,
                       Name = "Limited",
                       Description = "Limited Role"
                   });

        var grantedPermissionNames = await roleService.GetRolePermissionsAsync(role.Id);
        if (!grantedPermissionNames.Contains(Allow.Seo_View))
        {
            var permission = permissions.Single(candidate => candidate.Name == Allow.Seo_View);
            await roleService.AssignPermissionAsync(role.Id, permission.Id);
        }

        return role.Id;
    }

    /// <summary>
    /// Creates a user with a role of its own holding every permission.
    /// </summary>
    private async Task<(Guid userId, Guid roleId)> CreateUserWithRoleAsync(List<Permission> permissions, string username, string roleName)
    {
        var role = await roleService.GetByNameAsync(roleName) ??
            await roleService.CreateAsync(new Role { SystemCreated = true, Name = roleName });
        var rolePermissions = await permissionService.GetRolePermissionsAsync(role.Id);
        var permissionsToAssign = permissions.Where(p => !rolePermissions.Any(rp => rp.Name == p.Name)).ToList();
        // One round trip for the whole set: a role here holds every permission, and one save per row
        // was a few hundred round trips paid by every test run.
        if (permissionsToAssign.Count > 0)
        {
            await roleService.AssignPermissionsAsync(role.Id, permissionsToAssign.Select(p => p.Id).ToList());
        }

        var userId = await CreateAccountAsync(username, role.Id);
        return (userId, role.Id);
    }

    /// <summary>
    /// Creates an active account, when it is absent, holding the role named.
    /// </summary>
    private async Task<Guid> CreateAccountAsync(string username, Guid roleId)
    {
        var user = await userService.GetByUsernameAsync(username) ??
            await userService.CreateAsync(new User { SystemCreated = true, Username = username, Email = $"{username}@example.com" }, TestUsers.DefaultPassword);
        if (roleId != Guid.Empty && !await userService.IsInRoleAsync(user.Id, roleId))
        {
            await userService.AssignRoleAsync(user.Id, roleId);
        }

        return user.Id;
    }

    /// <summary>
    /// Writes the record behind every seeded profile image, so that an authenticated read of seeded data
    /// resolves to a file rather than to a missing one. The record is account-owned: a profile image
    /// belongs to the account.
    /// </summary>
    private async Task SeedStoredFilesForProfileImagesAsync()
    {
        var images = await dbContext.Users
            .Where(account => account.Image != null && account.Image != "")
            .Select(account => new { account.Id, Image = account.Image! })
            .ToListAsync();

        if (images.Count == 0)
        {
            return;
        }

        var recordedFileNames = await dbContext.StoredFiles
            .Select(storedFile => storedFile.FileName)
            .ToListAsync();

        var missing = images.Where(image => !recordedFileNames.Contains(image.Image)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        dbContext.StoredFiles.AddRange(missing.Select(image => new StoredFile
        {
            OwnerUserId = image.Id,
            FileName = image.Image,
            OriginalFileName = image.Image,
            ContentType = "application/octet-stream"
        }));
        await dbContext.SaveChangesAsync();
    }
}
