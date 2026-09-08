namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleListEndpoint"/> covering listing and pagination of roles.
/// </summary>
public class RoleListTests(App app) : AppTestsBase(app)
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
}
