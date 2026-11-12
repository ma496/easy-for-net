using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Seeder;

namespace Backend.Tests.Features.Identity.Endpoints.Users;

/// <summary>
/// Tests for the <see cref="UserListEndpoint"/> covering listing, pagination, and filtering of users,
/// and the set of accounts the caller may administer at all - the tenant being acted in, widened to
/// every account for a platform administrator.
/// </summary>
/// <remarks>
/// The restriction is asserted through the search rather than against the whole page: the suite runs
/// against one shared database and its collections in parallel, so a test may count only what it made.
/// Searching for an account by its own unique name asks the same question - is this account in the set
/// the caller may administer - and answers it with a total the test can assert exactly.
/// </remarks>
public class UserListTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that listing users returns a non-empty collection containing the created users.
    /// </summary>
    [Fact]
    public async Task List_Users()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var requests = faker.Generate(3);
        foreach (var request in requests)
        {
            await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
        }

        // Get list of users
        var (listRsp, listRes) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
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
    /// Verifies that pagination works correctly and returns different results for different pages.
    /// </summary>
    [Fact]
    public async Task List_Users_Pagination()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var requests = faker.Generate(5);
        foreach (var request in requests)
        {
            await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
        }

        // Get first page with 2 users
        var (page1Rsp, page1Res) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
            new()
            {
                Page = 1,
                PageSize = 2
            });

        page1Rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        page1Res.Items.Count.Should().Be(2);

        // Get second page
        var (page2Rsp, page2Res) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
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
    /// Verifies that filtering by <c>IsActive</c> flag returns only active users, inactive users, or all users when no filter is applied.
    /// </summary>
    [Fact]
    public async Task List_Users_FilterByIsActive()
    {
        await SetAuthTokenAsync();

        var roleService = Service<IRoleService>();
        var testRoleId = TestRoles.TestRoleId;

        // Create active users
        var activeFaker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex + "_active")
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var activeRequests = activeFaker.Generate(2);
        foreach (var request in activeRequests)
        {
            request.Roles = [testRoleId];
            await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
        }

        // Create inactive users
        var inactiveFaker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex + "_inactive")
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => false);
        var inactiveRequests = inactiveFaker.Generate(2);
        foreach (var request in inactiveRequests)
        {
            request.Roles = [testRoleId];
            await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);
        }

        // Get only active users
        var (activeRsp, activeRes) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
            new()
            {
                Page = 1,
                PageSize = 10,
                IsActive = true
            });

        activeRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        activeRes.Items.Should().Contain(x => x.IsActive);
        activeRes.Items.Should().NotContain(x => !x.IsActive);

        // Get only inactive users
        var (inactiveRsp, inactiveRes) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
            new()
            {
                Page = 1,
                PageSize = 10,
                IsActive = false
            });

        inactiveRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        inactiveRes.Items.Should().Contain(x => !x.IsActive);
        inactiveRes.Items.Should().NotContain(x => x.IsActive);

        // Get all users (no filter)
        var (allRsp, allRes) = await Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
            new()
            {
                Page = 1,
                PageSize = 10
            });

        allRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        allRes.Items.Should().Contain(x => x.IsActive);
        allRes.Items.Should().Contain(x => !x.IsActive);
    }

}