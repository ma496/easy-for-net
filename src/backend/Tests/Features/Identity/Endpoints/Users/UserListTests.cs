namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;
using Backend.Tests.Seeder;

/// <summary>
/// Tests for the <see cref="UserListEndpoint"/> covering listing, pagination, and filtering of users,
/// and the set of accounts the caller may administer at all - the tenant being acted in, widened to
/// every account for a platform administrator (AC-093, AC-095).
/// </summary>
/// <remarks>
/// The restriction is asserted through the search rather than against the whole page: the suite runs
/// against one shared database and its collections in parallel, so a test may count only what it made.
/// Searching for an account by its own unique name asks the same question - is this account in the set
/// the caller may administer - and answers it with a total the test can assert exactly.
/// </remarks>
public class UserListTests(App app) : TenancyTestsBase(app)
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

    /// <summary>
    /// Verifies that a caller acting in a tenant administers that tenant's accounts and no others -
    /// neither the page nor the count taken with it (AC-093).
    /// </summary>
    [Fact]
    public async Task Users_Are_Restricted_To_The_Active_Tenant()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_View));
        var stranger = await CreateTenantUserAsync(
            other.Id, await CreateTenantRoleAsync(other.Id, Allow.User_View));

        var client = await ClientForAsync(administrator.Username);

        // The account of the other tenant, searched for by the unique name it holds.
        var (foreignRsp, foreign) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = stranger.Username });

        foreignRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        foreign.Items.Should().BeEmpty("the caller administers the accounts of the tenant it acts in, and this account belongs to another");
        foreign.Total.Should().Be(0, "the count is taken over the same restricted set the page is drawn from, so an account of another tenant is not merely paged off it");

        // The caller's own account, which is in that set and proves the search above asked the question
        // it was supposed to ask rather than matching nothing at all.
        var (ownRsp, own) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = administrator.Username });

        ownRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        own.Total.Should().Be(1);
        own.Items.Select(item => item.Id).Should().Equal([administrator.Id]);
    }

    /// <summary>
    /// Verifies that a platform account administers every tenant's accounts by entering the tenant it
    /// means, and administers the platform's own while it is acting in none (AC-095).
    /// </summary>
    /// <remarks>
    /// The two halves are the whole of what the tier gives. Acting in no tenant, the accounts the list
    /// is about are the platform's own, so a tenant's member is not among them; entering a tenant it
    /// belongs to, they are that tenant's members and no other tenant's, read on the tenant role its
    /// membership holds there. Belonging to several tenants is not a view across all of them at once -
    /// which is why the caller below reaches both tenants' accounts in turn and never both together. That an ordinary caller is
    /// restricted the same way is stated by
    /// <see cref="Users_Are_Restricted_To_The_Active_Tenant"/>.
    /// </remarks>
    [Fact]
    public async Task Platform_Account_Administers_Each_Tenants_Accounts_By_Entering_It()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var viewInFirst = await CreateTenantRoleAsync(first.Id, Allow.User_View);
        var viewInSecond = await CreateTenantRoleAsync(second.Id, Allow.User_View);
        var inFirst = await CreateTenantUserAsync(first.Id, viewInFirst);
        var inSecond = await CreateTenantUserAsync(second.Id, viewInSecond);

        // The platform account belongs to both tenants, holding the view role in each: a membership is
        // what admits it to a tenant, and the tenant role is what it acts on there.
        var platformAccount = await CreateAccountWithoutMembershipAsync();
        await UserService.AssignRoleAsync(platformAccount.Id, TestRoles.PlatformAdminRoleId);
        await MarkAsPlatformAccountAsync(platformAccount.Id);
        await MembershipService.AddAsync(first.Id, platformAccount.Id, [viewInFirst], TestContext.Current.CancellationToken);
        await MembershipService.AddAsync(second.Id, platformAccount.Id, [viewInSecond], TestContext.Current.CancellationToken);

        await SignInAsAsync(platformAccount.Username);

        var (platformRsp, platformPage) = await Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = inFirst.Username });

        platformRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        platformPage.Items.Should().BeEmpty(
            "acting in no tenant the list is about the platform's own accounts, and this one belongs to a tenant");

        await SwitchTenantAsync(first.Id);

        var (firstRsp, firstPage) = await Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = inFirst.Username });

        firstRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        firstPage.Items.Select(item => item.Id).Should().Equal([inFirst.Id],
            "entering a tenant it belongs to puts the caller among that tenant's actors, on the role it holds there");

        var (selfRsp, selfPage) = await Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = platformAccount.Username });

        selfRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        selfPage.Total.Should().Be(0,
            "a platform account is administered from platform scope only, so its membership of the tenant does not put it on the tenant's list");

        var (strangerRsp, strangerPage) = await Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = inSecond.Username });

        strangerRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        strangerPage.Items.Should().BeEmpty(
            "and inside that tenant it is that tenant's actor, so another tenant's accounts are as absent as they are to anybody there");

        await SwitchTenantAsync(second.Id);

        var (secondRsp, secondPage) = await Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 100, Search = inSecond.Username });

        secondRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        secondPage.Items.Select(item => item.Id).Should().Equal([inSecond.Id],
            "the second tenant is reached the same way, through the membership held there");
    }
}