namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="RoleUpdateEndpoint"/> covering updating roles, non-existent roles,
/// protected system-created roles, and the role of another tenant that cannot be renamed.
/// </summary>
public class RoleUpdateTests(App app) : TenancyTestsBase(app)
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

        // The bootstrap tenant's administrator role, read across tenants because the row carries the
        // tenant it belongs to and the lookup here is the seeder's rather than a caller's.
        var systemCreatedRole = await DbContext.Roles
            .AcrossAllTenants()
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
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeUpdated);
    }

    /// <summary>
    /// Verifies that renaming a role belonging to another tenant answers exactly as renaming a role that
    /// does not exist does, and leaves it as it was.
    /// </summary>
    /// <remarks>
    /// The name asked for is one the caller would be allowed to take in its own tenant, so the only
    /// reason the request is refused is the tenant the role belongs to - and the same refusal is what a
    /// caller would get for an identifier naming nothing. The name and permission set are read back
    /// afterwards, because the two halves of the claim are separate: the answer must be indistinguishable
    /// from a miss, and the row must be untouched rather than merely not reported.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_Role_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.Role_View, Allow.Role_Update));
        var stranger = await CreateTenantRoleAsync(other.Id, Allow.Role_View);

        var before = await RoleAsync(stranger);
        var beforePermissions = await RolePermissionIdsAsync(stranger);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(new()
            {
                Id = stranger,
                Name = "Renamed From Another Tenant",
                Description = "Renamed from outside the tenant the role belongs to"
            });

        var (unknown, _) = await client
            .PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(new()
            {
                Id = Guid.NewGuid(),
                Name = "Renamed From Another Tenant",
                Description = "Renamed from outside the tenant the role belongs to"
            });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "a role of another tenant and a role that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes a role the caller may not rename from one that is not there");

        var after = await RoleAsync(stranger);
        after.Name.Should().Be(before.Name, "the role of another tenant still holds the name it was given");
        after.Description.Should().Be(before.Description, "and the description the request asked to replace");
        after.UpdatedBy.Should().BeNull("the rename was refused before anything reached the row");
        (await RolePermissionIdsAsync(stranger)).Should().BeEquivalentTo(beforePermissions,
            "and it still grants exactly what it granted");
    }

    /// <summary>
    /// Verifies that renaming a role onto a name its own tenant already uses is refused with a defined
    /// code naming the field to change, compared without regard to case or surrounding whitespace, and
    /// that the role being renamed is left exactly as it was.
    /// </summary>
    [Fact]
    public async Task Renaming_Onto_A_Name_The_Tenant_Already_Uses_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_Update));
        var client = await ClientForAsync(administrator.Username);

        var takenName = (await RoleAsync(await CreateTenantRoleAsync(tenant.Id, Allow.Role_View))).Name;
        var renamed = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var before = await RoleAsync(renamed);

        var (refused, problem) = await client
            .PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, ProblemDetails>(new()
            {
                Id = renamed,
                // Deliberately not the name as it is stored: uniqueness within the tenant is about the
                // name, not about how it happened to be typed.
                Name = $"  {takenName.ToUpperInvariant()}  ",
                Description = "A description the duplicate name has to refuse"
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.RoleNameAlreadyExists);
        problem.Errors.First().Name.Should().Be("name", "the caller is told which field to change");

        var after = await RoleAsync(renamed);

        after.Name.Should().Be(before.Name, "the refusal is the whole outcome of the request, so the role keeps its name");
        after.NameNormalized.Should().Be(before.NameNormalized);
        after.Description.Should().Be(before.Description, "and the description the request asked to replace");
        after.UpdatedBy.Should().BeNull("nothing reached the row to record the caller against it");
    }

    /// <summary>
    /// Verifies that the name a deleted role of the tenant was given is not free to take: renaming
    /// another role onto it is refused, because deleting a role does not release the name it used
    ///.
    /// </summary>
    /// <remarks>
    /// The deleted role is shown to be gone from the tenant's own view before the rename is attempted, so
    /// the refusal cannot be explained by the deleted role still being visible: the name is being held by
    /// a row the tenant no longer lists at all.
    /// </remarks>
    [Fact]
    public async Task Renaming_Onto_A_Deleted_Roles_Name_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_Update));
        var client = await ClientForAsync(administrator.Username);

        var deletedRole = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var deletedName = (await RoleAsync(deletedRole)).Name;
        await SoftDeleteRoleAsync(tenant.Id, deletedRole);

        (await RolesVisibleInAsync(tenant.Id))
            .Should().NotContain(role => role.Id == deletedRole,
                "the premise is that the tenant no longer lists the role whose name is about to be taken");

        var renamed = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
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
        problem.Errors.First().Code.Should().Be(ErrorCodes.RoleNameAlreadyExists);
        problem.Errors.First().Name.Should().Be("name");

        var after = await RoleAsync(renamed);

        after.Name.Should().Be(before.Name, "the name is held by the tenant's deleted role, so no live role of it can take the name");
    }

    /// <summary>
    /// Soft-deletes one role of a tenant, as deleting it through the surface would: the row is retained
    /// carrying the removal and the time it happened, and it stops satisfying every ordinary read.
    /// </summary>
    /// <param name="tenantId">The tenant the role belongs to.</param>
    /// <param name="roleId">The role to delete.</param>
    private async Task SoftDeleteRoleAsync(Guid tenantId, Guid roleId)
        => await TenantScopedAsync(tenantId, async () =>
        {
            var role = await DbContext.Roles.SingleAsync(
                x => x.Id == roleId, TestContext.Current.CancellationToken);

            role.IsDeleted = true;
            role.DeletedAt = DateTime.UtcNow;
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

    /// <summary>
    /// The roles a tenant lists to itself, read under a scope of that tenant so the soft-delete filter
    /// is in force exactly as it is for a caller acting there.
    /// </summary>
    /// <param name="tenantId">The tenant whose roles are read.</param>
    /// <returns>The roles the tenant lists.</returns>
    private async Task<List<Role>> RolesVisibleInAsync(Guid tenantId)
    {
        var roles = new List<Role>();

        await TenantScopedAsync(tenantId, async () => roles = await DbContext.Roles
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));

        return roles;
    }

    /// <summary>
    /// Reads a role back across every tenant, because what is asserted of it is a row a caller acting in
    /// another tenant must have been unable to rename.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

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
}
