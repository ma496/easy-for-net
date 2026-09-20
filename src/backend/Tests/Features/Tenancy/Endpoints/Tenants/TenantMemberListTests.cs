namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantMemberListEndpoint"/>: the page of accounts belonging to one tenant,
/// the paging, sorting, searching and role filtering it offers over them, and the caller it refuses
/// (AC-021, AC-061 - AC-064, AC-086).
/// </summary>
/// <remarks>
/// <para>
/// Every list here is scoped to a tenant the test created, so a total is exact without the suite's
/// shared database entering into it: a tenant made a moment ago has exactly the members the test put
/// in it and no others, whatever else is running in parallel.
/// </para>
/// <para>
/// The list is one of the two surfaces that address a tenant by route id rather than by the session -
/// the other is member addition - so the refusal it owes a caller with no standing in that tenant is
/// tested here, and the same refusal is tested from the other side in
/// <see cref="TenantMemberAddTests"/> (AC-021).
/// </para>
/// </remarks>
public class TenantMemberListTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that the member list is paged and that the total describes the whole matching set
    /// rather than the page returned, so an administration screen can show how many pages remain
    /// (AC-061).
    /// </summary>
    [Fact]
    public async Task List_Members_Pagination()
    {
        var tenant = await CreateTenantAsync();
        var members = await CreateMembersAsync(tenant.Id, count: 3);
        await SetPlatformAdminAuthTokenAsync();

        var (first, firstPage) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Page = 1, PageSize = 2, SortField = "Username", SortDirection = SortDirection.Asc });

        var (second, secondPage) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Page = 2, PageSize = 2, SortField = "Username", SortDirection = SortDirection.Asc });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        firstPage.Total.Should().Be(3, "the total counts every member of the tenant, not the page it returned");
        secondPage.Total.Should().Be(3);
        firstPage.Items.Should().HaveCount(2);
        secondPage.Items.Should().HaveCount(1);

        // The row identity is the member's account, which is what a later change of that member's roles
        // addresses, so the pages are compared by it rather than by a membership identity.
        firstPage.Items.Select(item => item.Id).Should().NotIntersectWith(secondPage.Items.Select(item => item.Id));
        firstPage.Items.Select(item => item.Id)
            .Concat(secondPage.Items.Select(item => item.Id))
            .Should().BeEquivalentTo(members.Select(member => member.Id), "the two pages together are the whole membership");
    }

    /// <summary>
    /// Verifies that the permitted sort fields order the member list, in either direction
    /// (AC-062).
    /// </summary>
    [Fact]
    public async Task Sorting()
    {
        var tenant = await CreateTenantAsync();
        var members = await CreateMembersAsync(tenant.Id, count: 3);
        await SetPlatformAdminAuthTokenAsync();

        var expected = members
            .OrderBy(member => member.Username, StringComparer.Ordinal)
            .Select(member => member.Id)
            .ToList();

        var (ascending, ascendingPage) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, All = true, SortField = "Username", SortDirection = SortDirection.Asc });

        var (descending, descendingPage) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, All = true, SortField = "Username", SortDirection = SortDirection.Desc });

        ascending.StatusCode.Should().Be(HttpStatusCode.OK);
        descending.StatusCode.Should().Be(HttpStatusCode.OK);
        ascendingPage.Items.Select(item => item.Id).Should().Equal(expected, "each account is named, so ordering by username is decidable rather than merely different");
        descendingPage.Items.Select(item => item.Id).Should().Equal(expected.AsEnumerable().Reverse());
    }

    /// <summary>
    /// Verifies that a sort field outside the permitted set is refused as a validation failure naming
    /// the field, rather than reaching the database and failing there (AC-063).
    /// </summary>
    [Fact]
    public async Task Invalid_Sort_Field()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, ProblemDetails>(
                new() { TenantId = tenant.Id, SortField = "PasswordHash" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(
            error => error.Name == "sortField",
            "sorting reflects over the account, so an unpermitted field has to be refused before it is applied");
    }

    /// <summary>
    /// Verifies that free-text search matches a member's username and email address, so the screen can
    /// find one member among many either way (AC-064).
    /// </summary>
    [Fact]
    public async Task Search_By_Username_And_Email()
    {
        var tenant = await CreateTenantAsync();
        var members = await CreateMembersAsync(tenant.Id, count: 2);
        var sought = members[0];
        var other = members[1];
        await SetPlatformAdminAuthTokenAsync();

        var (byUsername, byUsernamePage) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Search = sought.Username, All = true });

        var (byEmail, byEmailPage) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Search = sought.Email, All = true });

        byUsername.StatusCode.Should().Be(HttpStatusCode.OK);
        byEmail.StatusCode.Should().Be(HttpStatusCode.OK);
        byUsernamePage.Items.Select(item => item.Id).Should().Equal([sought.Id], "the username is unique to one account, so searching for it selects one member");
        byEmailPage.Items.Select(item => item.Id).Should().Equal([sought.Id], "and so does the address built from it");
        byUsernamePage.Items.Should().NotContain(item => item.Id == other.Id);
        byEmailPage.Items.Should().NotContain(item => item.Id == other.Id);
    }

    /// <summary>
    /// Verifies that the list can be filtered to the members holding one role of the tenant, which is
    /// how a screen answers who holds what (AC-086).
    /// </summary>
    [Fact]
    public async Task Filter_By_Role()
    {
        var tenant = await CreateTenantAsync();
        var holderRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var otherRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.TenantMember_View);
        var holder = await CreateTenantUserAsync(tenant.Id, holderRoleId);
        var other = await CreateTenantUserAsync(tenant.Id, otherRoleId);
        await SetPlatformAdminAuthTokenAsync();

        var (response, page) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, RoleId = holderRoleId, All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Total.Should().Be(1);
        page.Items.Select(item => item.Id).Should().Equal([holder.Id], "only the member holding the role filtered for is listed");
        page.Items.Should().NotContain(item => item.Id == other.Id);
    }

    /// <summary>
    /// Verifies that each row reports the member's account details, whether that account is active,
    /// when it joined, and the roles it holds in this tenant alone, so the screen renders a member
    /// without a second read (AC-086).
    /// </summary>
    [Fact]
    public async Task Rows_Report_The_Member_And_The_Tenant_Roles_It_Holds()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        await SetPlatformAdminAuthTokenAsync();

        var (response, page) = await Client
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                new() { TenantId = tenant.Id, Search = member.Username, All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = page.Items.Should().ContainSingle().Subject;

        row.Id.Should().Be(member.Id, "the row is addressed by the member's account, which is what changing its roles names");
        row.Username.Should().Be(member.Username);
        row.Email.Should().Be(member.Email);
        row.IsActive.Should().BeTrue();
        row.MemberSince.Should().BeCloseTo(
            DateTime.UtcNow,
            TimeSpan.FromMinutes(5),
            "the row reports when the tenant's membership began, which is the moment this test made it");
        row.Roles.Select(role => role.Id).Should().Equal([roleId], "the roles reported are the ones this tenant grants, and no other tenant's");
    }

    /// <summary>
    /// Verifies that a caller who is neither a platform administrator nor a member of the tenant
    /// addressed is refused with a defined code, and that the refusal carries no member rows - so the
    /// list cannot be used to read the membership of a tenant the caller has nothing to do with
    /// (AC-021).
    /// </summary>
    /// <remarks>
    /// The caller is given every permission the endpoint declares inside its own tenant, so what
    /// refuses it is standing rather than authority: a caller holding the permission elsewhere is
    /// still not a member here.
    /// </remarks>
    [Fact]
    public async Task Non_Member_Is_Refused()
    {
        var addressed = await CreateTenantAsync();

        var foreignTenant = await CreateTenantAsync();
        var foreignRoleId = await CreateTenantRoleAsync(foreignTenant.Id, Allow.TenantMember_View);
        var caller = await CreateTenantUserAsync(foreignTenant.Id, foreignRoleId);

        var callerClient = await ClientForAsync(caller.Username, foreignTenant.Id);

        var (response, problem) = await callerClient
            .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, ProblemDetails>(new() { TenantId = addressed.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.NotTenantMember, "holding the permission in another tenant is not standing in this one");
    }

    /// <summary>
    /// Creates members of one tenant, each with a username and an address no other account in the
    /// suite can hold.
    /// </summary>
    /// <param name="tenantId">The tenant the accounts are to join.</param>
    /// <param name="count">How many accounts to create.</param>
    /// <returns>The created accounts, in the order they were made.</returns>
    /// <remarks>
    /// No roles are granted: what each member holds is arranged by the test that cares, and a member
    /// holding nothing is a member the list has to report just the same.
    /// </remarks>
    private async Task<List<User>> CreateMembersAsync(Guid tenantId, int count)
    {
        var members = new List<User>();
        for (var index = 0; index < count; index++)
        {
            members.Add(await CreateTenantUserAsync(tenantId));
        }

        return members;
    }
}
