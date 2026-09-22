namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.Entities;
using Backend.ShareData.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantUpdateEndpoint"/>: renaming a tenant - its display name, its identifier,
/// or both - and the three refusals that answer a rename: a tenant the caller may not see, the
/// system-created bootstrap tenant, and an identifier another tenant already holds
/// (AC-003, AC-004, AC-005, AC-010, AC-011).
/// </summary>
/// <remarks>
/// <para>
/// A rename is the one tenant mutation that changes the value the uniqueness rule is enforced on, so
/// most of this class is about the identifier: that taking another's is refused and persists nothing,
/// that keeping one's own is not mistaken for taking it, and that a refusal leaves the row exactly as
/// it was. The display name is changed alongside it in the same request, so a refusal that slipped
/// through would be visible as a name that moved.
/// </para>
/// <para>
/// Every tenant asserted on is created by the test, and the bootstrap tenant - the one tenant the
/// seeder owns - is only ever read. Its rename is refused, which is what makes reading it safe.
/// </para>
/// </remarks>
// Shares a collection with TenantReactivateTests: both write the bootstrap tenant's row, and this
// class reads its UpdatedAt to prove a refused update changed nothing.
[Collection("BootstrapTenant")]
public class TenantUpdateTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row can
    /// relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that renaming a tenant stores the new name and identifier, and records who renamed it
    /// and when, so an administration screen shows the tenant as it now stands and the platform can
    /// answer who changed it (AC-005).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var identifier = NewTenantIdentifier();
        var (response, renamed) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(new()
            {
                Id = tenant.Id,
                Name = "Acme Holdings",
                Identifier = identifier
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        renamed.Id.Should().Be(tenant.Id);
        renamed.Name.Should().Be("Acme Holdings");
        renamed.Identifier.Should().Be(identifier);
        renamed.IdentifierNormalized.Should().Be(identifier, "the identifier is stored in the form the uniqueness comparison uses");

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Name.Should().Be("Acme Holdings");
        stored.Identifier.Should().Be(identifier);
        stored.Status.Should().Be(TenantStatus.Active, "a rename changes what a tenant is called, not what it is");
        stored.SystemCreated.Should().BeFalse();

        stored.UpdatedBy.Should().Be(TestUsers.PlatformAdminUserId, "the updating account is the caller that asked for the rename");
        stored.UpdatedAt.Should().NotBeNull("a rename is recorded with the time it happened");
        stored.UpdatedAt!.Value.Should().BeOnOrAfter(
            stored.CreatedAt,
            "the update time never precedes the creation it followed, which is what makes the audit readable in order");
    }

    /// <summary>
    /// Verifies that renaming a tenant onto an identifier another tenant already holds is refused
    /// against the identifier field, and that the refusal leaves the tenant exactly as it was - neither
    /// name nor identifier moved (AC-003).
    /// </summary>
    [Fact]
    public async Task Rename_To_Existing_Identifier()
    {
        var incumbent = await CreateTenantAsync();
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
            {
                Id = tenant.Id,
                Name = "Acme Holdings",
                Identifier = incumbent.Identifier
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle("the identifier is the one thing wrong with the request");
        problem.Errors.First().Name.Should().Be("identifier", "the caller is told which value was refused");
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Identifier.Should().Be(tenant.Identifier, "nothing was written before the guard passed, so the tenant keeps its own identifier");
        stored.Name.Should().Be(tenant.Name, "and the name it was refused along with");
    }

    /// <summary>
    /// Verifies that a tenant renamed while keeping the identifier it already holds is not refused as a
    /// duplicate of itself, which is what the comparison leaving the tenant out means (AC-003).
    /// </summary>
    [Fact]
    public async Task Keeping_Its_Own_Identifier_Is_Not_A_Duplicate()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, renamed) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(new()
            {
                Id = tenant.Id,
                Name = "Acme Holdings",
                Identifier = tenant.Identifier
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        renamed.Identifier.Should().Be(tenant.Identifier);

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Name.Should().Be("Acme Holdings", "the name is the part of the request that actually changed");
        stored.Identifier.Should().Be(tenant.Identifier);
    }

    /// <summary>
    /// Verifies that a rename breaking the naming rules is refused against each field that broke one,
    /// and that the tenant keeps the name and identifier it already had (AC-004).
    /// </summary>
    [Fact]
    public async Task Invalid_Input()
    {
        var tenant = await CreateTenantAsync();
        var before = await ReloadTenantAsync(tenant.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
            {
                Id = tenant.Id,
                Name = string.Empty,
                Identifier = "ab"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Name).Should().Equal(
            ["name", "identifier"],
            "both fields broke a rule, and each failure names the field it belongs to");

        var stored = await ReloadTenantAsync(tenant.Id);

        stored.Name.Should().Be(before.Name, "a request refused at validation reached no entity");
        stored.Identifier.Should().Be(before.Identifier);
        stored.UpdatedAt.Should().Be(before.UpdatedAt, "and it recorded no change, because it made none");
        stored.UpdatedBy.Should().Be(before.UpdatedBy);
    }

    /// <summary>
    /// Verifies that a request naming no tenant at all is refused before anything is read, so the last
    /// of this endpoint's failure branches is answered by validation rather than by a lookup of the
    /// empty identifier (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Tenant_Is_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
            {
                Id = Guid.Empty,
                Name = "Acme Holdings",
                Identifier = NewTenantIdentifier()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(error => error.Name == "id");
    }

    /// <summary>
    /// Verifies that the platform's own bootstrap tenant cannot be renamed, because the identifier it
    /// was created with is what the seeded data and the upgrade path are pinned to (AC-011).
    /// </summary>
    /// <remarks>
    /// The tenant is read but never written: the assertion is that the row the seeder made still carries
    /// the name it was seeded with, which is only meaningful while nothing else in the suite renames it.
    /// </remarks>
    [Fact]
    public async Task Cannot_Update_System_Created_Tenant()
    {
        var before = await ReloadTenantAsync(TestTenants.BootstrapTenantId);
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
            {
                Id = TestTenants.BootstrapTenantId,
                Name = "Renamed Bootstrap",
                Identifier = NewTenantIdentifier()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedTenantCannotBeModified);

        var after = await ReloadTenantAsync(TestTenants.BootstrapTenantId);

        after.Name.Should().Be(before.Name, "the bootstrap tenant keeps the name it was seeded with");
        after.Identifier.Should().Be(before.Identifier);
        after.UpdatedAt.Should().Be(before.UpdatedAt, "and the attempt left no trace on it");
    }

    /// <summary>
    /// Verifies that a tenant which has never existed and one that has been deleted are refused with
    /// the same answer, so a rename cannot be used to learn whether a tenant identifier was ever real
    /// (AC-010).
    /// </summary>
    [Fact]
    public async Task Deleted_And_Unknown_Tenant_Are_Refused_Alike()
    {
        var deleted = await CreateTenantAsync();
        await DeleteTenantAsync(deleted.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (deletedResponse, deletedProblem) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
            {
                Id = deleted.Id,
                Name = "Acme Holdings",
                Identifier = NewTenantIdentifier()
            });

        var (unknownResponse, unknownProblem) = await Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
            {
                Id = Guid.NewGuid(),
                Name = "Acme Holdings",
                Identifier = NewTenantIdentifier()
            });

        deletedResponse.StatusCode.Should().Be(unknownResponse.StatusCode, "the two are one answer, not two");
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deletedProblem.Errors.Should().ContainSingle();
        deletedProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        // Compared field by field rather than as whole bodies: each body carries a trace id of its own,
        // so only what describes the refusal is expected to be the same.
        deletedProblem.Errors.Select(error => (error.Name, error.Code, error.Reason))
            .Should().BeEquivalentTo(unknownProblem.Errors.Select(error => (error.Name, error.Code, error.Reason)));

        var stored = await ReloadRetainedTenantAsync(deleted.Id);

        stored.Name.Should().Be(deleted.Name, "a deleted tenant is not renamed on the way out");
        stored.Identifier.Should().Be(deleted.Identifier);
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
    /// retained rather than erased - which is exactly the row a refused rename leaves untouched.
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
    /// afterwards without the row having been erased - which is the state a rename has to be refused in.
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
