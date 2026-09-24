namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the tenant filter the <see cref="RoleListEndpoint"/> accepts: the narrowing it gives a
/// platform administrator who names a tenant and the refusal to widen a caller acting in a
/// tenant who names one.
/// </summary>
/// <remarks>
/// The domain is two tenants made by the test, each with a role of its own, because the question the
/// filter answers is which tenant's roles are wanted - and a caller can only be shown to be restricted
/// to the tenant it acts in if there is a second tenant whose roles it must not be shown.
/// </remarks>
public class RoleListTenantFilterTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a platform account acting in no tenant lists the platform's own roles, and reaches
    /// one tenant's by naming it.
    /// </summary>
    /// <remarks>
    /// The unfiltered call is made first so that what the filter does is legible: without it the list is
    /// the platform's own roles and holds neither tenant's, and naming a tenant is what brings that
    /// tenant's - and only that tenant's - into view. The filter is what the web client uses to offer
    /// the roles of the tenant whose members are being administered from the tenants table, so a filter
    /// that did nothing would leave that picker with nothing to offer at all.
    /// </remarks>
    [Fact]
    public async Task Platform_Account_Reaches_A_Named_Tenants_Roles()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var inFirst = await CreateTenantRoleAsync(first.Id, Allow.Role_View);
        var inSecond = await CreateTenantRoleAsync(second.Id, Allow.Role_View);

        await SetPlatformAdminAuthTokenAsync();

        var (wideRsp, wide) = await Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        wideRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var wideIds = wide.Items.Select(item => item.Id).ToList();
        wideIds.Should().NotContain(inFirst).And.NotContain(inSecond,
            "platform scope is about the roles belonging to no tenant, so naming a tenant is what reaches one's roles rather than narrowing a view that already held them");

        var (narrowedRsp, narrowed) = await Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true, TenantId = second.Id });

        narrowedRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var narrowedIds = narrowed.Items.Select(item => item.Id).ToList();
        narrowedIds.Should().Contain(inSecond, "the tenant named is the one whose roles were asked for");
        narrowedIds.Should().NotContain(inFirst, "and the other tenant's roles are not in the narrowed view");

        narrowed.Total.Should().Be(2,
            "the count follows the filter as the page does - this tenant's administrator role and the one role created in it");
    }

    /// <summary>
    /// Verifies that a caller acting in a tenant is answered from that tenant whichever tenant it names,
    /// so naming one can never widen its view.
    /// </summary>
    /// <remarks>
    /// The same request is made twice, once naming the caller's own tenant and once naming another's, and
    /// the two answers are compared to each other: a caller who cannot see the difference between naming
    /// its tenant and naming somebody else's is a caller the parameter does not reach. The roles it acts
    /// on are asserted present as well, because a filter that emptied the list for everybody would
    /// otherwise look like a restriction.
    /// </remarks>
    [Fact]
    public async Task Tenant_Caller_Is_Answered_From_The_Tenant_It_Acts_In()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var actedRoleId = await CreateTenantRoleAsync(acted.Id, Allow.Role_View);
        var otherRoleId = await CreateTenantRoleAsync(other.Id, Allow.Role_View);
        var administrator = await CreateTenantUserAsync(acted.Id, actedRoleId);

        var client = await ClientForAsync(administrator.Username);

        var (ownRsp, own) = await client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true, TenantId = acted.Id });

        ownRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (foreignRsp, foreign) = await client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true, TenantId = other.Id });

        foreignRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        foreign.Items.Select(item => item.Id).Should().BeEquivalentTo(
            own.Items.Select(item => item.Id),
            "the filter is ignored rather than honoured for this caller, so the tenant it names makes no difference to what it is shown");

        var ids = foreign.Items.Select(item => item.Id).ToList();
        ids.Should().Contain(actedRoleId, "the roles of the tenant the caller acts in are still the ones it is shown");
        ids.Should().NotContain(
            otherRoleId,
            "so naming another tenant cannot hand a caller the roles of a tenant it is not in, which is what would let it assign one of them");

        foreign.Total.Should().Be(
            2,
            "the count is taken over the tenant the caller acts in as well - its administrator role and the one role created there");
    }
}