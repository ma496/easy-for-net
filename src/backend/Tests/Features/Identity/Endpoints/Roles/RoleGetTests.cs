namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="RoleGetEndpoint"/> covering retrieval of existing and non-existent roles,
/// and the role of another tenant that is not there to be read (AC-111).
/// </summary>
public class RoleGetTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a created role can be retrieved by ID with the correct name and description.
    /// </summary>
    [Fact]
    public async Task Get_Role()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (createRsp, createRes) = await Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (getRsp, getRes) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.Name.Should().Be(request.Name);
        getRes.Description.Should().Be(request.Description);
        getRes.SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a role created by the seeder is reported as system-created so the UI can hide its actions.
    /// </summary>
    [Fact]
    public async Task Get_SystemCreated_Role()
    {
        await SetAuthTokenAsync();

        var (getRsp, getRes) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = TestRoles.AdminRoleId
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.SystemCreated.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that requesting a non-existent role returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_NonExistent_Role()
    {
        await SetAuthTokenAsync();

        var (getRsp, _) = await Client.GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that reading a role belonging to another tenant answers exactly as reading a role that
    /// does not exist does, and leaves it untouched (AC-111).
    /// </summary>
    /// <remarks>
    /// The two answers are compared as one: the same status and the same body, so not even their shape
    /// tells a caller whether the role it named belongs to another tenant or to nobody - and the role's
    /// name is asserted absent from the refusal, so the refusal does not confirm what it refused to
    /// show. The stored name and permission set are compared afterwards because a read that had reached
    /// the row on its way to being refused would be a different answer wearing the same costume.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_Role_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.Role_View));
        var stranger = await CreateTenantRoleAsync(other.Id, Allow.Role_View);

        var before = await RoleAsync(stranger);
        var beforePermissions = await RolePermissionIdsAsync(stranger);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = stranger });

        var (unknown, _) = await client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = Guid.NewGuid() });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "a role of another tenant and a role that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes a role the caller may not read from one that is not there");
        refusedBody.Should().NotContain(before.Name,
            "so that the role named by the caller is not confirmed back to them by being refused");

        var after = await RoleAsync(stranger);
        after.Name.Should().Be(before.Name, "the role of another tenant was not read, and is exactly as it was");
        after.Description.Should().Be(before.Description);
        (await RolePermissionIdsAsync(stranger)).Should().BeEquivalentTo(beforePermissions,
            "and it still grants exactly what it granted, so nothing about the refused request reached it");
    }

    /// <summary>
    /// Reads a role back across every tenant, because what is asserted of it is a row the caller acting
    /// in another tenant must have been unable to touch.
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
