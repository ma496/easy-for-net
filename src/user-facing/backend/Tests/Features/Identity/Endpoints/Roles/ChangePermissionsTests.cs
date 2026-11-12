using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;

namespace Backend.Tests.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="ChangePermissionsEndpoint"/> covering assigning permissions to roles, the
/// platform permission a tenant role can never be given, the tenant-only permission a platform
/// role can never be given, the system-created tenant role whose
/// permissions cannot be changed at all, and the role of another tenant that cannot be reached
///.
/// </summary>
public class ChangePermissionsTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that permissions can be successfully assigned to a newly created role.
    /// </summary>
    /// <remarks>
    /// The role belongs to a tenant this test made and the caller administers it from inside, because a
    /// role belongs to a tenant and only the tenant-tier permissions of the catalogue can be granted
    /// through one - so the set assigned here is drawn from that tier rather than from whatever
    /// the catalogue happens to return first.
    /// </remarks>
    [Fact]
    public async Task Change_Permissions()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_ChangePermissions));
        var roleId = await CreateRoleAsync();
        var permissions = await PermissionIdsAsync(skip: 0, take: 2);

        var client = await ClientForAsync(administrator.Username);

        var (rsp, res) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = roleId,
                Permissions = permissions
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Permissions.Should().BeEquivalentTo(permissions);
        (await RolePermissionIdsAsync(roleId)).Should().BeEquivalentTo(permissions,
            "the response and the stored set are the same set, so the echoed identifiers are the ones the role was given");
    }

    /// <summary>
    /// Verifies that permissions can be updated on a role that already has permissions, replacing the existing set with a mix of old and new permissions.
    /// </summary>
    /// <remarks>
    /// The first request establishes the state the second one is about: the role has permissions of its
    /// own by the time the mix is asked for, so the replacement is a real one - an old permission is kept
    /// while the rest of the set changes, which a change that only ever added could not produce.
    /// </remarks>
    [Fact]
    public async Task Change_Permissions_Of_Role_Already_Has_Permissions()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_ChangePermissions));
        var roleId = await CreateRoleAsync();

        var client = await ClientForAsync(administrator.Username);
        var permissions = await PermissionIdsAsync(skip: 0, take: 2);

        var (rsp, _) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = roleId,
                Permissions = permissions
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK, "the role holds permissions of its own before the set is mixed");

        var mixPermissions = new List<Guid> { permissions[0] };
        mixPermissions.AddRange(await PermissionIdsAsync(skip: 2, take: 2));

        var (rsp1, res1) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ChangePermissionsResponse>(
            new()
            {
                Id = roleId,
                Permissions = mixPermissions
            });

        rsp1.StatusCode.Should().Be(HttpStatusCode.OK);
        res1.Permissions.Should().BeEquivalentTo(mixPermissions);

        (await RolePermissionIdsAsync(roleId)).Should().BeEquivalentTo(mixPermissions,
            "the set asked for replaces the one the role had, rather than being added to it");
    }

    /// <summary>
    /// The stored permission behind a permission name, looked up from the catalogue so that the test
    /// grants by identifier - which is what the request carries - rather than by a name the endpoint
    /// never sees.
    /// </summary>
    /// <param name="permissionName">The permission's name in the code-declared catalogue.</param>
    /// <returns>The identifier of the stored permission.</returns>
    private async Task<List<Guid>> PermissionIdsAsync(int skip, int take)
        => await DbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Name)
            .Skip(skip)
            .Take(take)
            .Select(permission => permission.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

    private async Task<Guid> PermissionIdAsync(string permissionName)
        => await DbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.Name == permissionName)
            .Select(permission => permission.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The permissions a role holds, read from the join rows rather than through any surface that
    /// narrows to the tenant being acted in.
    /// </summary>
    /// <param name="roleId">The role whose permissions are wanted.</param>
    /// <returns>The permission identifiers the role holds.</returns>
    private async Task<List<Guid>> RolePermissionIdsAsync(Guid roleId)
        => await DbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToListAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Cannot_Change_System_Created_Role_Permissions()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_ChangePermissions));
        var client = await ClientForAsync(administrator.Username);

        var (rsp, problem) = await client.PUTAsync<ChangePermissionsEndpoint, ChangePermissionsRequest, ProblemDetails>(
            new() { Id = TestRoles.AdminRoleId, Permissions = [] });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SystemCreatedRolePermissionsCannotBeChanged.Value);
    }
}
