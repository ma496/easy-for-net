namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantGetEndpoint"/>: reading one tenant, and the single refusal that answers
/// every way a tenant can be unavailable to the caller (AC-010, AC-021, AC-046).
/// </summary>
/// <remarks>
/// <para>
/// The point of the refusal is what it does not say. A tenant that was deleted, a tenant the caller
/// holds no standing in, and a tenant that never existed are deliberately one answer: the same status,
/// the same code and the same message, so the response cannot be used to learn whether a tenant
/// identifier is - or ever was - real (AC-010).
/// </para>
/// <para>
/// A tenant addressed by id is refused through <c>ThrowError</c>, which is a 400 carrying the code,
/// rather than through a bare 404. The code is the point: AC-069 turns it into a message, and a 404
/// without one would tell the web app nothing it could translate.
/// </para>
/// </remarks>
public class TenantGetTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a caller holding platform administration reads a tenant it holds no membership in
    /// (AC-046).
    /// </summary>
    [Fact]
    public async Task Platform_Administrator_Reads_A_Tenant_It_Is_Not_A_Member_Of()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, read) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, TenantGetResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        read.Id.Should().Be(tenant.Id);
        read.Name.Should().Be(tenant.Name);
        read.Identifier.Should().Be(tenant.Identifier);
        read.Status.Should().Be(TenantStatus.Active);
        read.SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a member reads the tenant it belongs to, which is the ordinary path the
    /// administration screens depend on (AC-021).
    /// </summary>
    [Fact]
    public async Task Member_Reads_Its_Own_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_Detail);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        await SignInAsAsync(member.Username, tenant.Id);

        var (response, read) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, TenantGetResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        read.Id.Should().Be(tenant.Id);
    }

    /// <summary>
    /// Verifies that the narrower grant admits a caller on its own: a member holding
    /// <see cref="Allow.Tenant_Detail"/> and not <see cref="Allow.Tenant_View"/> reads their own
    /// tenant, which is what lets a tenant administrator open their tenant's detail screen without
    /// being admitted to the platform's list of every tenant.
    /// </summary>
    [Fact]
    public async Task Detail_Grant_Alone_Reads_The_Callers_Own_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_Detail);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        await SignInAsAsync(member.Username, tenant.Id);

        var (response, read) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, TenantGetResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "either grant admits the caller, and this one is the grant a tenant administrator is given");
        read.Id.Should().Be(tenant.Id);
    }

    /// <summary>
    /// Verifies that the narrower grant widens who may ask and nothing else: a caller holding
    /// <see cref="Allow.Tenant_Detail"/> is still refused a tenant they have no standing in, with the
    /// same answer an absent tenant gets.
    /// </summary>
    [Fact]
    public async Task Detail_Grant_Does_Not_Reach_Another_Tenant()
    {
        var joined = await CreateTenantAsync();
        var stranger = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(joined.Id, Allow.Tenant_Detail);
        var member = await CreateTenantUserAsync(joined.Id, roleId);
        await SignInAsAsync(member.Username, joined.Id);

        var (refused, problem) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = stranger.Id });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "what a caller may read is decided by their standing in the tenant, never by which of the two grants admitted them");
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);
    }

    /// <summary>
    /// Verifies that a caller that is a member of one tenant and not of another is refused the other
    /// exactly as it is refused a tenant that is not there, so membership standing cannot be probed
    /// through this route (AC-021, AC-010).
    /// </summary>
    [Fact]
    public async Task Non_Member_Is_Refused_As_If_The_Tenant_Did_Not_Exist()
    {
        var joined = await CreateTenantAsync();
        var stranger = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(joined.Id, Allow.Tenant_Detail);
        var member = await CreateTenantUserAsync(joined.Id, roleId);
        await SignInAsAsync(member.Username, joined.Id);

        var (refused, problem) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = stranger.Id });

        refused.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "a tenant the caller has no standing in is refused with the code, so its existence cannot be probed");
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        // The name is what a response would leak if the tenant had been found and the refusal were
        // merely an authorization failure rather than the same answer an absent tenant gets.
        var body = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Contains(stranger.Name).Should().BeFalse("nothing about the tenant the caller may not see is revealed");
    }

    /// <summary>
    /// Verifies that a tenant which has been deleted is refused exactly as a tenant that never existed
    /// is, which is what makes a deletion unobservable (AC-010).
    /// </summary>
    [Fact]
    public async Task Deleted_Tenant_Is_Refused_As_If_The_Tenant_Did_Not_Exist()
    {
        var deleted = await CreateTenantAsync();
        await DeleteTenantAsync(deleted.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (deletedResponse, deletedProblem) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = deleted.Id });

        var (unknownResponse, unknownProblem) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = Guid.NewGuid() });

        deletedResponse.StatusCode.Should().Be(unknownResponse.StatusCode, "the two are one answer, not two");
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deletedProblem.Errors.Should().ContainSingle();
        deletedProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        // Compared field by field rather than as whole bodies: the two bodies are not byte-identical,
        // because each carries a trace id of its own, but nothing about the tenant differs between them.
        deletedProblem.Errors.Select(error => (error.Name, error.Code, error.Reason))
            .Should().BeEquivalentTo(unknownProblem.Errors.Select(error => (error.Name, error.Code, error.Reason)));

        var deletedBody = await deletedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        deletedBody.Contains(deleted.Name).Should().BeFalse("a deleted tenant is not even named back to the caller");
    }

    /// <summary>
    /// Verifies that a tenant that has never existed is refused with the not-found code (AC-010).
    /// </summary>
    [Fact]
    public async Task Unknown_Tenant_Is_Not_Found()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);
    }

    /// <summary>
    /// Verifies that reading the platform's own bootstrap tenant reports it as system-created, which is
    /// the fact every refusal to rename, suspend or delete it rests on (AC-046).
    /// </summary>
    [Fact]
    public async Task Bootstrap_Tenant_Is_System_Created()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, read) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, TenantGetResponse>(
                new() { Id = TestTenants.BootstrapTenantId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        read.SystemCreated.Should().BeTrue("the bootstrap tenant is the one the platform provisions for itself");
        read.Status.Should().Be(TenantStatus.Active);
    }

    /// <summary>
    /// Verifies that a request naming no tenant at all is refused before anything is read (AC-086).
    /// </summary>
    [Fact]
    public async Task Missing_Tenant_Is_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = Guid.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(error => error.Name == "id");
    }

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
