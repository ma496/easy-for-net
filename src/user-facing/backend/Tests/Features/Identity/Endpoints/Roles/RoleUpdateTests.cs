using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;

namespace Backend.Tests.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleUpdateEndpoint"/> covering updating roles, non-existent roles,
/// protected system-created roles, and the name a deleted role still holds.
/// </summary>
public class RoleUpdateTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that a created role can be successfully updated with new name and description.
    /// </summary>
    [Fact]
    public async Task Update_Role()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (createRsp, createRes) = await Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateFaker = new Faker<RoleUpdateRequest>()
            .RuleFor(x => x.Id, f => createRes.Id)
            .RuleFor(x => x.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(x => x.Description, f => f.Lorem.Sentence());
        var updateRequest = updateFaker.Generate();
        var (updateRsp, updateRes) = await Client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(updateRequest);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        updateRes.Name.Should().Be(updateRequest.Name);
        updateRes.Description.Should().Be(updateRequest.Description);
    }

    /// <summary>
    /// Verifies that updating a non-existent role returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Update_NonExistent_Role()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleUpdateRequest>()
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (updateRsp, _) = await Client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(request);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that updating the system-created Admin role returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedRoleCannotBeUpdated"/>.
    /// </summary>
    [Fact]
    public async Task Update_Admin_Role_Should_Fail()
    {
        await SetAuthTokenAsync();

        // The seeded administrator role, read straight from the database because the lookup here is
        // the seeder's rather than a caller's.
        var systemCreatedRole = await DbContext.Roles
            .AsNoTracking()
            .SingleAsync(role => role.Id == TestRoles.AdminRoleId, TestContext.Current.CancellationToken);
        systemCreatedRole.SystemCreated.Should().BeTrue("the premise of this test is that the role is one the seeder made");

        var faker = new Faker<RoleUpdateRequest>()
            .RuleFor(u => u.Id, f => systemCreatedRole.Id)
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (updateRsp, res) = await Client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, ProblemDetails>(request);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeUpdated.Value);
    }

    /// <summary>
    /// Verifies that the name a deleted role was given is not free to take: renaming another role onto
    /// it is refused, because deleting a role does not release the name it used.
    /// </summary>
    /// <remarks>
    /// The deleted role is shown to be gone from the role list before the rename is attempted, so
    /// the refusal cannot be explained by the deleted role still being visible: the name is being held by
    /// a row no longer listed at all.
    /// </remarks>
    [Fact]
    public async Task Renaming_Onto_A_Deleted_Roles_Name_Is_Refused()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_Update));
        var client = await ClientForAsync(administrator.Username);

        var deletedRole = await CreateRoleAsync(Allow.Role_View);
        var deletedName = (await RoleAsync(deletedRole)).Name;
        await SoftDeleteRoleAsync(deletedRole);

        (await RolesVisibleAsync())
            .Should().NotContain(role => role.Id == deletedRole,
                "the premise is that the role list no longer holds the role whose name is about to be taken");

        var renamed = await CreateRoleAsync(Allow.User_View);
        var before = await RoleAsync(renamed);

        var (refused, problem) = await client
            .PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, ProblemDetails>(new()
            {
                Id = renamed,
                Name = deletedName,
                Description = "A description the deleted role's name has to refuse"
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.RoleNameAlreadyExists.Value);
        problem.Errors.First().Name.Should().Be("name");

        var after = await RoleAsync(renamed);

        after.Name.Should().Be(before.Name, "the name is held by the deleted role, so no live role can take the name");
    }

    /// <summary>
    /// Soft-deletes one role, as deleting it through the surface would: the row is retained carrying the
    /// removal and the time it happened, and it stops satisfying every ordinary read.
    /// </summary>
    /// <param name="roleId">The role to delete.</param>
    private async Task SoftDeleteRoleAsync(Guid roleId)
    {
        var role = await DbContext.Roles.SingleAsync(x => x.Id == roleId, TestContext.Current.CancellationToken);

        role.IsDeleted = true;
        role.DeletedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The roles the soft-delete filter lets through.
    /// </summary>
    /// <returns>The live roles.</returns>
    private async Task<List<Role>> RolesVisibleAsync()
        => await DbContext.Roles
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Reads a role back straight from the database, so that what is asserted of it does not depend on
    /// the endpoint under test.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

    /// <summary>
    /// The permissions a role holds, read from the join rows rather than through the endpoint under test.
    /// </summary>
    /// <param name="roleId">The role whose permissions are wanted.</param>
    /// <returns>The permission identifiers the role holds.</returns>
    private async Task<List<Guid>> RolePermissionIdsAsync(Guid roleId)
        => await DbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToListAsync(TestContext.Current.CancellationToken);
}
