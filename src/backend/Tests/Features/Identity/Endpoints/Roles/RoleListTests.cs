namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="RoleListEndpoint"/> covering listing and pagination of roles, the roles a
/// caller acting in a tenant is shown (AC-110) and the widening a platform administrator is given
/// instead (AC-113).
/// </summary>
public class RoleListTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that listing roles returns a non-empty collection containing the created roles.
    /// </summary>
    [Fact]
    public async Task List_Roles()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var requests = faker.Generate(3);
        foreach (var request in requests)
        {
            await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);
        }

        // Get list of roles
        var (listRsp, listRes) = await App.Client.GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(
            new()
            {
                Page = 1,
                PageSize = 10
            });

        listRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        listRes.Items.Should().NotBeEmpty();
        listRes.Items.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    /// <summary>
    /// Verifies that list rows carry the system-created flag, so the admin UI can hide the actions on seeded roles.
    /// </summary>
    [Fact]
    public async Task List_Roles_Reports_SystemCreated()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var (createRsp, createRes) = await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(faker.Generate());

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (listRsp, listRes) = await App.Client.GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(
            new()
            {
                All = true
            });

        listRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        listRes.Items.Single(x => x.Id == TestRoles.AdminRoleId).SystemCreated.Should().BeTrue();
        listRes.Items.Single(x => x.Id == createRes.Id).SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that pagination works correctly and returns different results for different pages.
    /// </summary>
    [Fact]
    public async Task List_Roles_Pagination()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var requests = faker.Generate(5);
        foreach (var request in requests)
        {
            await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);
        }

        // Get first page with 2 roles
        var (page1Rsp, page1Res) = await App.Client.GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(
            new()
            {
                Page = 1,
                PageSize = 2
            });

        page1Rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        page1Res.Items.Count.Should().Be(2);

        // Get second page
        var (page2Rsp, page2Res) = await App.Client.GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(
            new()
            {
                Page = 2,
                PageSize = 2
            });

        page2Rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Res.Items.Count.Should().Be(2);
        page2Res.Should().NotBeEquivalentTo(page1Res);
    }

    /// <summary>
    /// Verifies that a caller acting in a tenant lists that tenant's roles and no others, and that the
    /// total is taken over the same restricted set (AC-110).
    /// </summary>
    /// <remarks>
    /// The caller administers one generated tenant and is a member of no other, while the two roles it
    /// must not see are the ones that would appear if the restriction were dropped: a role of another
    /// tenant, and the platform administrator role that belongs to no tenant at all. The count is asked
    /// for as well as the page, because a total drawn from a wider set than the rows is the same leak
    /// told in a number - it would report how many roles the installation keeps.
    /// </remarks>
    [Fact]
    public async Task Roles_Are_Restricted_To_The_Active_Tenant()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var actedRoleId = await CreateTenantRoleAsync(acted.Id, Allow.Role_View, Allow.Role_Create);
        var otherRoleId = await CreateTenantRoleAsync(other.Id, Allow.Role_View);
        var administrator = await CreateTenantUserAsync(acted.Id, actedRoleId);

        var client = await ClientForAsync(administrator.Username);

        var (response, page) = await client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ids = page.Items.Select(item => item.Id).ToList();

        ids.Should().Contain(actedRoleId, "the role the caller administers here is one of the roles it lists");
        ids.Should().NotContain(
            otherRoleId,
            "a role of another tenant is not in the set this caller administers, whether it reaches the page or not");
        ids.Should().NotContain(
            TestRoles.PlatformAdminRoleId,
            "the platform administrator role belongs to no tenant, so it is outside every tenant's list - including the one this caller acts in");

        page.Total.Should().Be(
            2,
            "the count is taken over the same restricted set the page is drawn from - this tenant's administrator role and the one role the caller made");
    }

    /// <summary>
    /// Verifies that a caller holding platform administration lists the roles of every tenant, without
    /// being a member of any of them (AC-113).
    /// </summary>
    /// <remarks>
    /// The two tenants are made by the test and the seeded administrator is a member of neither, so what
    /// the two ids appearing together proves is the platform tier itself rather than an account that
    /// happens to belong to both. Every other assertion in this class is the same list read by a caller
    /// who is not a platform administrator, which is what makes this one a widening rather than a
    /// property of the endpoint.
    /// </remarks>
    [Fact]
    public async Task Platform_Administrator_Sees_Roles_Across_Tenants()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var inFirst = await CreateTenantRoleAsync(first.Id, Allow.Role_View);
        var inSecond = await CreateTenantRoleAsync(second.Id, Allow.Role_View);

        await SignInAsPlatformAdministratorActingInATenantAsync();

        var (response, page) = await App.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ids = page.Items.Select(item => item.Id).ToList();

        ids.Should().Contain(inFirst, "the caller administers roles irrespective of the tenant they belong to");
        ids.Should().Contain(
            inSecond,
            "and irrespective of membership: the caller belongs to neither of these tenants, and reads both");
    }
}
