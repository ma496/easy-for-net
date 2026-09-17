namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantSuspendEndpoint"/>: putting a tenant out of service, the data it keeps
/// while out of service, and the two requests it refuses - the system-created bootstrap tenant and one
/// naming a tenant that is not there (AC-006, AC-011, AC-086).
/// </summary>
/// <remarks>
/// <para>
/// Suspension is a lifecycle change and nothing else, so the assertions are as much about what was left
/// alone as about the status: the tenant's own row, the roles inside it and its memberships all survive,
/// which is what lets reactivation restore a tenant rather than rebuild one.
/// </para>
/// <para>
/// What suspension <em>does</em> to a member's requests is not tested here but in
/// <see cref="TenantSuspensionTests"/>, because it is enforced centrally for every tenant-scoped
/// endpoint rather than by this one.
/// </para>
/// </remarks>
public class TenantSuspendTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row can
    /// relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that suspending an active tenant sets it to suspended and retains everything it owns -
    /// the rows inside it are neither deleted nor detached, so nothing has to be restored later beyond
    /// the status itself (AC-006).
    /// </summary>
    [Fact]
    public async Task Suspend_Active_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        await SetPlatformAdminAuthTokenAsync();

        var (response, suspended) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        suspended.Id.Should().Be(tenant.Id);
        suspended.Status.Should().Be(TenantStatus.Suspended, "the response reports the state the tenant was put into");

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Status.Should().Be(TenantStatus.Suspended);
        stored.IsDeleted.Should().BeFalse("a suspended tenant is out of service, not deleted");

        // The row inside the tenant is read across tenants because the tenant it belongs to is what the
        // tenant filter uses to hide it - and hiding it is exactly what suspension must not do.
        var retainedRole = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

        retainedRole.Should().NotBeNull("suspension retains the tenant's rows unchanged");
        retainedRole!.IsDeleted.Should().BeFalse();
        retainedRole.TenantId.Should().Be(tenant.Id, "and leaves them attributed to the tenant they belong to");
    }

    /// <summary>
    /// Verifies that suspending a tenant that is already suspended is accepted and changes nothing, so
    /// the operation describes a state to reach rather than a transition that can fail (AC-006).
    /// </summary>
    [Fact]
    public async Task Suspending_An_Already_Suspended_Tenant_Is_Accepted()
    {
        var tenant = await CreateTenantAsync(TenantStatus.Suspended);
        var before = await ReloadTenantAsync(tenant.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, suspended) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        suspended.Status.Should().Be(TenantStatus.Suspended);

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Status.Should().Be(TenantStatus.Suspended);
        stored.UpdatedAt.Should().Be(before.UpdatedAt, "the second suspension asks for the state the tenant is already in");
    }

    /// <summary>
    /// Verifies that the platform's own bootstrap tenant cannot be suspended, because the application
    /// depends on it being in service, and that the attempt leaves it active (AC-011).
    /// </summary>
    [Fact]
    public async Task Cannot_Suspend_System_Created_Tenant()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, ProblemDetails>(
                new() { Id = TestTenants.BootstrapTenantId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedTenantCannotBeModified);

        var stored = await ReloadTenantAsync(TestTenants.BootstrapTenantId);

        stored.Status.Should().Be(TenantStatus.Active, "the refusal happened before anything was written");
        stored.SystemCreated.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a tenant which has never existed and one that has been deleted are refused with the
    /// same answer, so suspension cannot be used to learn whether a tenant identifier was ever real
    /// (AC-086).
    /// </summary>
    [Fact]
    public async Task Unknown_And_Deleted_Tenant_Are_Refused_Alike()
    {
        var deleted = await CreateTenantAsync();
        await DeleteTenantAsync(deleted.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (deletedResponse, deletedProblem) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, ProblemDetails>(new() { Id = deleted.Id });

        var (unknownResponse, unknownProblem) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, ProblemDetails>(new() { Id = Guid.NewGuid() });

        deletedResponse.StatusCode.Should().Be(unknownResponse.StatusCode, "the two are one answer, not two");
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deletedProblem.Errors.Should().ContainSingle();
        deletedProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        deletedProblem.Errors.Select(error => (error.Name, error.Code, error.Reason))
            .Should().BeEquivalentTo(unknownProblem.Errors.Select(error => (error.Name, error.Code, error.Reason)));

        var retained = await ReloadRetainedTenantAsync(deleted.Id);

        retained.Status.Should().Be(
            TenantStatus.Active,
            "a deleted tenant is not suspended on the way out: the tenant filter is what hides it, and the row is left as it was");
    }

    /// <summary>
    /// Verifies that a request naming no tenant at all is refused before anything is read, which is the
    /// last of this endpoint's failure branches (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Tenant_Is_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, ProblemDetails>(new() { Id = Guid.Empty });

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
