using Backend.Features.Tenancy.Core.Entities;

namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantReactivateEndpoint"/>: returning a suspended tenant to service, and the
/// requests it refuses - which is only ever one naming a tenant that is not there (AC-008, AC-086).
/// </summary>
/// <remarks>
/// <para>
/// Reactivation has nothing to restore beyond the status, because suspension removed nothing, so the
/// assertions are about the tenant coming back to the state it was left in and about the tenants this
/// endpoint deliberately does not refuse: one that is already active and the system-created bootstrap
/// tenant, which can never be suspended and so is never in need of reactivation.
/// </para>
/// <para>
/// That a reactivated tenant's members work again without signing in again is proved in
/// <see cref="TenantSuspensionTests"/>, where the token that was refused is the token that is admitted.
/// </para>
/// </remarks>
// Shares a collection with TenantUpdateTests - see the note there.
[Collection("BootstrapTenant")]
public class TenantReactivateTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row can
    /// relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that reactivating a suspended tenant returns it to the active state, which is what
    /// restores normal access for its members without any action on their part (AC-008).
    /// </summary>
    [Fact]
    public async Task Reactivate_Suspended_Tenant()
    {
        var tenant = await CreateTenantAsync(TenantStatus.Suspended);
        await SetPlatformAdminAuthTokenAsync();

        var (response, reactivated) = await Client
            .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        reactivated.Id.Should().Be(tenant.Id);
        reactivated.Status.Should().Be(TenantStatus.Active, "the response reports the state the tenant was returned to");

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Status.Should().Be(TenantStatus.Active);
        stored.IsDeleted.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that reactivating a tenant that is already active is accepted and changes nothing, so
    /// the operation describes a state to reach rather than a transition that can fail (AC-008).
    /// </summary>
    [Fact]
    public async Task Reactivating_An_Active_Tenant_Is_Accepted()
    {
        var tenant = await CreateTenantAsync();
        var before = await ReloadTenantAsync(tenant.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, reactivated) = await Client
            .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        reactivated.Status.Should().Be(TenantStatus.Active);

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Status.Should().Be(TenantStatus.Active);
        stored.UpdatedAt.Should().Be(before.UpdatedAt, "the second reactivation asks for the state the tenant is already in");
    }

    /// <summary>
    /// Verifies that reactivating the system-created bootstrap tenant is accepted rather than refused,
    /// because a tenant that can never be suspended is never in need of reactivation - which is why this
    /// endpoint carries no system-created guard while its siblings do (AC-008, AC-011).
    /// </summary>
    [Fact]
    public async Task Reactivating_The_System_Created_Tenant_Is_Accepted()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, reactivated) = await Client
            .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(
                new() { Id = TestTenants.BootstrapTenantId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await ReloadTenantAsync(TestTenants.BootstrapTenantId);

        stored.Status.Should().Be(TenantStatus.Active, "the bootstrap tenant was active and is still active");
        stored.SystemCreated.Should().BeTrue();
        reactivated.Status.Should().Be(TenantStatus.Active);
    }

    /// <summary>
    /// Verifies that a tenant which has never existed and one that has been deleted are refused with the
    /// same answer, so reactivation cannot be used to learn whether a tenant identifier was ever real
    /// (AC-086).
    /// </summary>
    [Fact]
    public async Task Unknown_And_Deleted_Tenant_Are_Refused_Alike()
    {
        var deleted = await CreateTenantAsync();
        await DeleteTenantAsync(deleted.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (deletedResponse, deletedProblem) = await Client
            .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, ProblemDetails>(new() { Id = deleted.Id });

        var (unknownResponse, unknownProblem) = await Client
            .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, ProblemDetails>(new() { Id = Guid.NewGuid() });

        deletedResponse.StatusCode.Should().Be(unknownResponse.StatusCode, "the two are one answer, not two");
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deletedProblem.Errors.Should().ContainSingle();
        deletedProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        deletedProblem.Errors.Select(error => (error.Name, error.Code, error.Reason))
            .Should().BeEquivalentTo(unknownProblem.Errors.Select(error => (error.Name, error.Code, error.Reason)));

        var retained = await ReloadRetainedTenantAsync(deleted.Id);

        retained.IsDeleted.Should().BeTrue(
            "rehabilitation is not resurrection: a deleted tenant stays deleted however its status reads");
    }

    /// <summary>
    /// Verifies that a request naming no tenant at all is refused before anything is read, which is the
    /// last of this endpoint's failure branches (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Tenant_Is_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, ProblemDetails>(new() { Id = Guid.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(error => error.Name == "id");
    }

    /// <summary>
    /// Reads a tenant back from the database rather than from the change tracker, so an assertion is
    /// about what was persisted and not about the instance the test arranged through.
    /// </summary>
    /// <param name="tenantId">The tenant being read.</param>
    /// <returns>The stored tenant.</returns>
    private async Task<Tenant> ReloadTenantAsync(Guid tenantId)
        => await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(tenant => tenant.Id == tenantId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Reads a tenant that has been retired, so both filters are relaxed: the tenant one because a
    /// tenant belongs to no tenant of its own, and the soft-delete one because a retired tenant is
    /// retained rather than erased.
    /// </summary>
    /// <param name="tenantId">The tenant being read.</param>
    /// <returns>The retained tenant.</returns>
    private async Task<Tenant> ReloadRetainedTenantAsync(Guid tenantId)
        => await DbContext.Tenants
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .SingleAsync(tenant => tenant.Id == tenantId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Retires a tenant the way the platform surface does, so that it reads as absent to every caller
    /// afterwards without the row having been erased.
    /// </summary>
    /// <param name="tenantId">The tenant being retired.</param>
    private async Task DeleteTenantAsync(Guid tenantId)
    {
        using var platformScope = TenantContext.BeginPlatformScope();

        var tenant = await DbContext.Tenants
            .SingleAsync(candidate => candidate.Id == tenantId, TestContext.Current.CancellationToken);

        DbContext.Tenants.Remove(tenant);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
