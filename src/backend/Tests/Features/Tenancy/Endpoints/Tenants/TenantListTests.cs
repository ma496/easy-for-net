namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantListEndpoint"/>: which tenants a caller is shown, and the paging,
/// sorting, searching and filtering the list offers over them (AC-046, AC-061 - AC-066).
/// </summary>
/// <remarks>
/// <para>
/// The class is built around one question - which tenants does this caller see - and the answer now
/// begins before the list: reading every tenant there is belongs to the platform scope, so the
/// permission that opens this surface can only be exercised by a platform account acting in no tenant
/// (AC-046). A caller acting inside a tenant is refused it outright and learns which tenants it
/// belongs to from the account-info call instead, which reports exactly its active memberships
/// (AC-066).
/// </para>
/// <para>
/// Isolation is by search rather than by reading the whole list, because the suite shares one database
/// and runs its collections in parallel: every tenant these tests create carries a name and an
/// identifier derived from one token unique to the test, and each assertion narrows the list by that
/// token first. A total is then exact while still being about nothing but this test's rows.
/// </para>
/// </remarks>
public class TenantListTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a caller holding platform administration sees every tenant, including ones it
    /// holds no membership in and has never acted in (AC-046).
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Sees_Tenants_It_Is_Not_A_Member_Of()
    {
        var token = NewSearchToken();
        var created = await CreateTenantsAsync(token, count: 2);
        await SetPlatformAdminAuthTokenAsync();

        var (response, page) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(new() { Search = token, All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Total.Should().Be(2, "both tenants are visible to a platform administrator, joined or not");
        page.Items.Select(item => item.Id).Should().BeEquivalentTo(created.Select(tenant => tenant.Id));
    }

    /// <summary>
    /// Verifies that a caller acting inside a tenant is refused this surface altogether, whatever its
    /// own role names: the permission that opens it is exercisable only in platform scope, so a tenant
    /// role granting it grants nothing and the list cannot be used to discover a tenant the caller has
    /// no standing in (AC-066).
    /// </summary>
    /// <remarks>
    /// The role is deliberately given the permission the endpoint asks for. That is what makes the
    /// refusal say something: the caller is not short of a grant, it is in the wrong scope to exercise
    /// the one it has, and a session carries only the permissions of the scope it acts in.
    /// </remarks>
    [Fact]
    public async Task Caller_Acting_In_A_Tenant_Is_Refused_The_List()
    {
        var token = NewSearchToken();
        var joined = await CreateTenantsAsync(token, count: 1);
        var roleId = await CreateTenantRoleAsync(joined[0].Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(joined[0].Id, roleId);

        await SignInAsAsync(member.Username, joined[0].Id);

        var (refused, _) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(new() { Search = token, All = true });

        refused.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the tenants list is the platform's own surface, so a caller acting inside a tenant cannot exercise the permission that opens it");
    }

    /// <summary>
    /// Verifies that the list is paged and that the total describes the whole matching set rather than
    /// the page (AC-061).
    /// </summary>
    [Fact]
    public async Task List_Tenants_Pagination()
    {
        var token = NewSearchToken();
        var created = await CreateTenantsAsync(token, count: 3);
        await SetPlatformAdminAuthTokenAsync();

        var (first, firstPage) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = token, Page = 1, PageSize = 2, SortField = "Identifier" });

        var (second, secondPage) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = token, Page = 2, PageSize = 2, SortField = "Identifier" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        firstPage.Total.Should().Be(3, "the total counts every tenant the request matched, not the page it returned");
        secondPage.Total.Should().Be(3);
        firstPage.Items.Should().HaveCount(2);
        secondPage.Items.Should().HaveCount(1);
        firstPage.Items.Select(item => item.Id).Should().NotIntersectWith(secondPage.Items.Select(item => item.Id));
        firstPage.Items.Select(item => item.Id)
            .Concat(secondPage.Items.Select(item => item.Id))
            .Should().BeEquivalentTo(created.Select(tenant => tenant.Id), "the two pages together are the whole set");
    }

    /// <summary>
    /// Verifies that a page size beyond the documented maximum is refused as a validation failure
    /// rather than served, which is what bounds the page (AC-061).
    /// </summary>
    [Fact]
    public async Task Page_Size_Beyond_The_Maximum_Is_Refused()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, ProblemDetails>(new() { PageSize = 101 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(
            error => error.Name == "pageSize",
            "the maximum page size is declared, so a larger one is a validation failure the client can report");
    }

    /// <summary>
    /// Verifies that the permitted sort fields order the list, in either direction (AC-062).
    /// </summary>
    [Fact]
    public async Task Sorting()
    {
        var token = NewSearchToken();
        var created = await CreateTenantsAsync(token, count: 3);
        await SetPlatformAdminAuthTokenAsync();

        var (ascending, ascendingPage) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = token, All = true, SortField = "Name", SortDirection = SortDirection.Asc });

        var (descending, descendingPage) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = token, All = true, SortField = "Name", SortDirection = SortDirection.Desc });

        ascending.StatusCode.Should().Be(HttpStatusCode.OK);
        descending.StatusCode.Should().Be(HttpStatusCode.OK);
        ascendingPage.Items.Select(item => item.Id).Should().Equal(
            created.Select(tenant => tenant.Id),
            "the tenants are named in the order they were created, so sorting by name ascending is that order");
        descendingPage.Items.Select(item => item.Id).Should().Equal(
            created.Select(tenant => tenant.Id).Reverse(),
            "and descending is its exact reverse");
    }

    /// <summary>
    /// Verifies that a sort field outside the permitted set is refused as a validation failure rather
    /// than reaching the database (AC-063).
    /// </summary>
    [Fact]
    public async Task Invalid_Sort_Field()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, ProblemDetails>(new() { SortField = "PasswordHash" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(
            error => error.Name == "sortField",
            "sorting reflects over the entity, so an unpermitted field has to be refused before it is applied");
    }

    /// <summary>
    /// Verifies that free-text search matches a tenant's display name and its identifier, and that the
    /// identifier is matched irrespective of the case it is typed in, which is what the stored
    /// normalized form is for (AC-064).
    /// </summary>
    [Fact]
    public async Task Search_By_Name_And_Identifier()
    {
        var token = NewSearchToken();
        var created = await CreateTenantsAsync(token, count: 1);
        await SetPlatformAdminAuthTokenAsync();

        // The name carries the token whole and the identifier only carries it as a prefix, so a search
        // that matched one and not the other would be missing from one of these results.
        var (byName, named) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(new() { Search = token, All = true });

        var (byIdentifier, identified) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = $"{token}-0", All = true });

        var (byUpperCase, upperCased) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = $"{token}-0".ToUpperInvariant(), All = true });

        byName.StatusCode.Should().Be(HttpStatusCode.OK);
        byIdentifier.StatusCode.Should().Be(HttpStatusCode.OK);
        byUpperCase.StatusCode.Should().Be(HttpStatusCode.OK);
        named.Items.Select(item => item.Id).Should().Equal([created[0].Id], "the token appears in the display name");
        identified.Items.Select(item => item.Id).Should().Equal([created[0].Id], "the token appears in the identifier");
        upperCased.Items.Select(item => item.Id).Should().Equal(
            [created[0].Id],
            "the identifier is compared in its normalized form, so the case it was typed in does not decide the match");
    }

    /// <summary>
    /// Verifies that the list can be filtered by lifecycle status (AC-065).
    /// </summary>
    [Fact]
    public async Task Filter_By_Status()
    {
        var token = NewSearchToken();
        var active = await CreateTenantsAsync(token, count: 1);
        var suspended = await CreateTenantsAsync(token, count: 1, status: TenantStatus.Suspended);
        await SetPlatformAdminAuthTokenAsync();

        var (response, page) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = token, All = true, Status = TenantStatus.Suspended });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Total.Should().Be(1, "only the suspended tenant matches the filter, though both match the search");
        page.Items.Select(item => item.Id).Should().Equal([suspended[0].Id]);
        page.Items.Select(item => item.Id).Should().NotContain(active[0].Id);
    }

    /// <summary>
    /// Verifies that each row reports the tenant's lifecycle status, whether the platform created it,
    /// and its identifier in the form the uniqueness comparison uses, so an administration screen can
    /// render a suspended or system-created tenant without a second read (AC-065, AC-086).
    /// </summary>
    [Fact]
    public async Task List_Reports_The_Status_And_System_Creation_Of_Each_Tenant()
    {
        var token = NewSearchToken();
        var created = await CreateTenantsAsync(token, count: 1, status: TenantStatus.Suspended);
        await SetPlatformAdminAuthTokenAsync();

        var (response, page) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(new() { Search = token, All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = page.Items.Should().ContainSingle().Subject;
        row.Id.Should().Be(created[0].Id);
        row.Status.Should().Be(TenantStatus.Suspended);
        row.SystemCreated.Should().BeFalse("a tenant created through the service is not the platform's own bootstrap tenant");
        row.IdentifierNormalized.Should().Be($"{token}-0", "the identifier travels in the form the uniqueness comparison uses");
    }

    /// <summary>
    /// A token no other test and no other run of the suite can collide with, used as the shared prefix
    /// of every tenant a single test creates.
    /// </summary>
    /// <returns>The token.</returns>
    private static string NewSearchToken() => $"tl{Guid.NewGuid():N}";

    /// <summary>
    /// Creates tenants whose name and identifier both carry the token, so that narrowing the list by it
    /// selects exactly these tenants.
    /// </summary>
    /// <param name="token">The token every created tenant is named with.</param>
    /// <param name="count">How many tenants to create.</param>
    /// <param name="status">The lifecycle state to leave each tenant in.</param>
    /// <returns>The created tenants, in the order they were made.</returns>
    /// <remarks>
    /// The ordinal that distinguishes the tenants is counted across the whole test rather than from zero
    /// on each call. Two tests here create their tenants in two calls - one to have a tenant of the
    /// caller's own and one to have a tenant of somebody else's - and a per-call ordinal would name the
    /// first tenant of the second call exactly as it named the first tenant of the first one. The
    /// identifier is unique across soft-deleted rows, so that collision is not merely a duplicate result:
    /// the insert is refused, and the refused tenant stays tracked as pending in the shared context and
    /// is retried by every later save. One reused ordinal is enough to fail the rest of the class.
    /// </remarks>
    private async Task<List<Tenant>> CreateTenantsAsync(string token, int count, TenantStatus status = TenantStatus.Active)
    {
        // Platform scope, because a tenant belongs to no tenant of its own: this is the standing a
        // tenant is created from, and the one the creation endpoint acts under.
        using var platformScope = TenantContext.BeginPlatformScope();

        var tenants = new List<Tenant>();
        for (var index = 0; index < count; index++)
        {
            var ordinal = _tenantOrdinal++;

            var tenant = await TenantService.CreateAsync(new Tenant
            {
                // Named so that ascending order by name is the order they were created in, which is what
                // lets the sort test state an expected order rather than merely that it changed.
                Name = $"{token} {ordinal}",
                Identifier = $"{token}-{ordinal}"
            }, cancellationToken: TestContext.Current.CancellationToken);

            if (status != TenantStatus.Active)
            {
                tenant.Status = status;
                await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            tenants.Add(tenant);
        }

        return tenants;
    }

    /// <summary>
    /// How many tenants this test has created, which is what makes each one's name and identifier its own.
    /// </summary>
    private int _tenantOrdinal;
}