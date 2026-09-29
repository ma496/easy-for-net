namespace Backend.Tests.FeatureManagement;

using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Editions;
using Backend.Features.Tenancy.Endpoints.FeatureValues;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tests.Features.Identity;

/// <summary>
/// Tests for the sessions a plan change ends: moving a tenant onto another edition, changing a tenant's
/// feature values, changing an edition's feature values and deleting an edition. Each change ends every
/// session acting in the tenants it names - the access token answers 401 with no renewal and the refresh
/// token is refused - and leaves the sessions of every other tenant, and every session in platform scope,
/// working.
/// </summary>
/// <remarks>
/// Every tenant, edition and account is made by the test itself, and the change is made through the
/// endpoint by a platform administrator, so no seeded tenant, edition or account is ever changed.
/// </remarks>
public class PlanChangeSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies that moving a tenant onto another edition ends every session in it, and leaves a tenant
    /// still on the old edition, the same account's session there and a platform session working.
    /// </summary>
    [Fact]
    public async Task Changing_A_Tenants_Edition_Ends_Every_Session_In_That_Tenant_And_No_Other()
    {
        var oldEdition = await CreateEditionAsync();
        var newEdition = await CreateEditionAsync();
        var moved = await CreateTenantOnEditionAsync(oldEdition.Id);
        var stays = await CreateTenantOnEditionAsync(oldEdition.Id);
        var (inMoved, elsewhere) = await ArrangeSessionsAsync([moved], [stays]);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                new() { Id = moved.Id, Name = moved.Name, Identifier = moved.Identifier, EditionId = newEdition.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllEndedAsync(inMoved);
        await AssertAllAliveAsync(elsewhere);
    }

    /// <summary>
    /// Verifies that taking a tenant off its edition, onto no plan at all, is a plan change like any
    /// other and ends every session in it.
    /// </summary>
    [Fact]
    public async Task Taking_A_Tenant_Off_Its_Edition_Ends_Every_Session_In_That_Tenant_And_No_Other()
    {
        var edition = await CreateEditionAsync();
        var moved = await CreateTenantOnEditionAsync(edition.Id);
        var stays = await CreateTenantOnEditionAsync(edition.Id);
        var (inMoved, elsewhere) = await ArrangeSessionsAsync([moved], [stays]);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                new() { Id = moved.Id, Name = moved.Name, Identifier = moved.Identifier, EditionId = null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllEndedAsync(inMoved);
        await AssertAllAliveAsync(elsewhere);
    }

    /// <summary>
    /// Verifies that an update leaving the tenant on the edition it was on - a rename - ends no session.
    /// </summary>
    [Fact]
    public async Task Renaming_A_Tenant_On_The_Same_Edition_Ends_No_Session()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        var (inTenant, _) = await ArrangeSessionsAsync([tenant], []);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                new() { Id = tenant.Id, Name = $"Renamed {Guid.NewGuid():N}", Identifier = NewTenantIdentifier(), EditionId = edition.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllAliveAsync(inTenant);
    }

    /// <summary>
    /// Verifies that changing a tenant's feature values ends every session in it, and leaves another
    /// tenant, the same account's session there and a platform session working.
    /// </summary>
    [Fact]
    public async Task Changing_A_Tenants_Feature_Values_Ends_Every_Session_In_That_Tenant_And_No_Other()
    {
        var changed = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var (inChanged, elsewhere) = await ArrangeSessionsAsync([changed], [other]);

        var response = await SetFeatureAsync(FeatureValueProviderNames.Tenant, changed.Id, "false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllEndedAsync(inChanged);
        await AssertAllAliveAsync(elsewhere);
    }

    /// <summary>
    /// Verifies that switching a feature back on is a plan change like switching it off: it ends the
    /// sessions minted while it was off, so the permissions it gates return from the next sign-in.
    /// </summary>
    [Fact]
    public async Task Switching_A_Tenants_Feature_Back_On_Ends_Every_Session_In_That_Tenant_And_No_Other()
    {
        var changed = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        (await SetFeatureAsync(FeatureValueProviderNames.Tenant, changed.Id, "false")).StatusCode.Should().Be(HttpStatusCode.OK);
        var (inChanged, elsewhere) = await ArrangeSessionsAsync([changed], [other]);

        var response = await SetFeatureAsync(FeatureValueProviderNames.Tenant, changed.Id, "true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllEndedAsync(inChanged);
        await AssertAllAliveAsync(elsewhere);
    }

    /// <summary>
    /// Verifies that changing an edition's feature values ends every session in every tenant on that
    /// edition, and leaves a tenant on another edition, the same account's session there and a platform
    /// session working.
    /// </summary>
    [Fact]
    public async Task Changing_An_Editions_Feature_Values_Ends_Every_Session_In_Every_Tenant_On_It_And_No_Other()
    {
        var changedEdition = await CreateEditionAsync();
        var otherEdition = await CreateEditionAsync();
        var first = await CreateTenantOnEditionAsync(changedEdition.Id);
        var second = await CreateTenantOnEditionAsync(changedEdition.Id);
        var onOther = await CreateTenantOnEditionAsync(otherEdition.Id);
        var (onChanged, elsewhere) = await ArrangeSessionsAsync([first, second], [onOther]);

        var response = await SetFeatureAsync(FeatureValueProviderNames.Edition, changedEdition.Id, "false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllEndedAsync(onChanged);
        await AssertAllAliveAsync(elsewhere);
    }

    /// <summary>
    /// Verifies that renaming an edition, which leaves what it is worth untouched, ends no session in the
    /// tenants on it.
    /// </summary>
    [Fact]
    public async Task Renaming_An_Edition_Ends_No_Session()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        var (inTenant, _) = await ArrangeSessionsAsync([tenant], []);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .PUTAsync<EditionUpdateEndpoint, EditionUpdateRequest, EditionUpdateResponse>(
                new() { Id = edition.Id, Name = $"Edition {Guid.NewGuid():N}", Description = "Renamed", DisplayOrder = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllAliveAsync(inTenant);
    }

    /// <summary>
    /// Verifies that deleting an edition leaves the sessions of tenants on other editions, and a platform
    /// session, working. An edition any live tenant is on cannot be deleted, so no tenant in this test is
    /// on the deleted one.
    /// </summary>
    [Fact]
    public async Task Deleting_An_Edition_Ends_No_Session_In_A_Tenant_It_Does_Not_Name()
    {
        var deleted = await CreateEditionAsync();
        var kept = await CreateEditionAsync();
        var onKept = await CreateTenantOnEditionAsync(kept.Id);
        var (_, elsewhere) = await ArrangeSessionsAsync([], [onKept]);

        await SetPlatformAdminAuthTokenAsync();
        var (response, _) = await Client
            .DELETEAsync<EditionDeleteEndpoint, EditionDeleteRequest, EditionDeleteResponse>(new() { Id = deleted.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertAllAliveAsync(elsewhere);
    }

    /// <summary>
    /// Verifies that a deletion the endpoint refuses because a tenant is still on the edition ends no
    /// session in that tenant.
    /// </summary>
    [Fact]
    public async Task Refused_Edition_Deletion_Ends_No_Session()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        var (inTenant, _) = await ArrangeSessionsAsync([tenant], []);

        await SetPlatformAdminAuthTokenAsync();
        var (response, problem) = await Client
            .DELETEAsync<EditionDeleteEndpoint, EditionDeleteRequest, ProblemDetails>(new() { Id = edition.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.EditionInUse.Value);

        await AssertAllAliveAsync(inTenant);
    }

    #region Helpers

    /// <summary>
    /// Signs in two members of every named tenant, a member of every unnamed one, an account belonging to
    /// the first of each - so the same account holds a session on both sides - and a platform account
    /// acting in no tenant.
    /// </summary>
    /// <param name="named">The tenants the change names, whose sessions must end.</param>
    /// <param name="unnamed">The tenants the change does not name, whose sessions must survive.</param>
    /// <returns>The sessions in the named tenants, and every session that must survive the change.</returns>
    private async Task<(List<RenewableSession> InNamed, List<RenewableSession> Elsewhere)> ArrangeSessionsAsync(
        IReadOnlyList<Tenant> named,
        IReadOnlyList<Tenant> unnamed)
    {
        var inNamed = new List<RenewableSession>();
        var elsewhere = new List<RenewableSession>();

        foreach (var tenant in named)
        {
            inNamed.Add(await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id));
            inNamed.Add(await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id));
        }

        foreach (var tenant in unnamed)
        {
            elsewhere.Add(await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id));
        }

        if (named.Count > 0 && unnamed.Count > 0)
        {
            var dual = await CreateDualTenantMemberAsync(named[0].Id, unnamed[0].Id);
            inNamed.Add(await SessionForAsync(dual.Username, named[0].Id));
            elsewhere.Add(await SessionForAsync(dual.Username, unnamed[0].Id));
        }

        var platformAccount = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(platformAccount.Id);
        elsewhere.Add(await SessionForAsync(platformAccount.Username));

        return (inNamed, elsewhere);
    }

    /// <summary>
    /// Switches account administration to a value for a tenant or an edition through the endpoint, as a
    /// platform administrator.
    /// </summary>
    private async Task<HttpResponseMessage> SetFeatureAsync(string providerName, Guid providerKey, string value)
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .PUTAsync<FeatureValueUpdateEndpoint, FeatureValueUpdateRequest, FeatureValueUpdateResponse>(new()
            {
                ProviderName = providerName,
                ProviderKey = providerKey.ToString(),
                Features = [new() { Name = FeatureNames.Identity_UserManagement, Value = value }]
            });

        return response;
    }

    private async Task AssertAllEndedAsync(IEnumerable<RenewableSession> sessions)
    {
        foreach (var session in sessions)
        {
            await AssertEndedAsync(session);
        }
    }

    private async Task AssertAllAliveAsync(IEnumerable<RenewableSession> sessions)
    {
        foreach (var session in sessions)
        {
            await AssertAliveAsync(session);
        }
    }

    #endregion
}
