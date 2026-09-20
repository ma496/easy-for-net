using Backend.Features.Tenancy.Core.Entities;

namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using System.Text;
using Backend.Data.Entities;
using Backend.Features.FileManagement.Core;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Tests for <see cref="TenantDeleteEndpoint"/>: retiring a tenant, the rows it keeps while becoming
/// unreachable, the files it goes on holding while no longer serving them, and the refusals it answers -
/// the system-created bootstrap tenant, a tenant already deleted and one that is not there
/// (AC-009, AC-011, AC-060, AC-079, AC-086).
/// </summary>
/// <remarks>
/// <para>
/// A deleted tenant disappears from every surface and from no storage: the row stays, its identifier
/// stays reserved, and everything attributed to it stays attributed to it. What the deletion does is
/// make the tenant unreachable - no session can name it and pass the session check, and no query of
/// the tenant surfaces can return it - which is why the assertions pair "the API cannot see it" with
/// "the database still holds it".
/// </para>
/// <para>
/// Nothing is cascaded. Deleting a tenant does not delete, soft-delete or detach the rows inside it,
/// so the test proves the row that was in the tenant is still there, undeleted, naming the tenant it
/// belongs to.
/// </para>
/// </remarks>
public class TenantDeleteTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row can
    /// relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Verifies that deleting a tenant soft-deletes it - excluded from every query while remaining
    /// retained in storage - and that the rows inside it are retained too, undeleted and still
    /// attributed to it (AC-009, AC-079).
    /// </summary>
    [Fact]
    public async Task Delete_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        await SetPlatformAdminAuthTokenAsync();

        var (response, deleted) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        deleted.Success.Should().BeTrue("the tenant was retired");

        // Excluded from every query: neither reading it nor searching the list finds it, which is what
        // a caller can observe of a deletion.
        var (readResponse, readProblem) = await Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, ProblemDetails>(new() { Id = tenant.Id });

        readResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        readProblem.Errors.Should().ContainSingle();
        readProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        var (listResponse, page) = await Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(
                new() { Search = tenant.Identifier, All = true });

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Items.Should().BeEmpty("a deleted tenant matches no list query, however exactly it is searched for");

        // Retained in storage: the row is there with both filters relaxed, carrying the deletion rather
        // than having been erased, so nothing the tenant owned has been orphaned or lost.
        var retained = await ReloadRetainedTenantAsync(tenant.Id);

        retained.IsDeleted.Should().BeTrue("a deletion is recorded on the row rather than performed on it");
        retained.DeletedAt.Should().NotBeNull("and it is recorded with the time it happened");
        retained.Identifier.Should().Be(tenant.Identifier);

        var retainedRole = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleOrDefaultAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

        retainedRole.Should().NotBeNull("deleting a tenant cascades to nothing inside it");
        retainedRole!.IsDeleted.Should().BeFalse();
        retainedRole.TenantId.Should().Be(tenant.Id, "and leaves what was in the tenant attributed to it");
    }

    /// <summary>
    /// Verifies that a file uploaded in a tenant is retained through the tenant's deletion: the record
    /// still attributes it to the tenant and the bytes are still in storage, so a deletion takes a tenant
    /// out of service rather than taking its data away (AC-060).
    /// </summary>
    /// <remarks>
    /// This is the retention half of AC-060, stated over the tenant's own files where the deletion
    /// happens; <c>FileGetTests</c> is the half about what a caller is answered, and
    /// <c>TenantSuspensionTests</c> states both for suspension.
    /// </remarks>
    [Fact]
    public async Task Deleted_Tenant_Keeps_Its_Files()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var memberClient = await ClientForAsync(member.Username, tenant.Id);

        var content = "the tenant's own file";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var upload = new FileUploadRequest
        {
            File = new FormFile(stream, 0, stream.Length, "file", "notes.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            },
            AccountOwned = false
        };

        var (uploadResponse, uploaded) = await memberClient
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(upload, sendAsFormData: true);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK, "the upload acts in the tenant the member's session names");

        var storageProvider = Service<IStorageProvider>();

        await SetPlatformAdminAuthTokenAsync();

        var (response, deleted) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        deleted.Success.Should().BeTrue();

        var storedFile = await DbContext.StoredFiles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(file => file.FileName == uploaded.FileName, TestContext.Current.CancellationToken);

        storedFile.TenantId.Should().Be(tenant.Id, "the file stays attributed to the tenant it was uploaded in");
        storedFile.OriginalFileName.Should().Be("notes.txt", "the record still describes the file the tenant uploaded, rather than being detached from it");
        storageProvider.Exists(uploaded.FileName).Should().BeTrue("and the content itself is retained rather than cleaned up");
    }

    /// <summary>
    /// Verifies that the platform's own bootstrap tenant cannot be deleted, because the application and
    /// the seeded data are pinned to it, and that the attempt leaves it in place (AC-011).
    /// </summary>
    [Fact]
    public async Task Cannot_Delete_System_Created_Tenant()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, ProblemDetails>(
                new() { Id = TestTenants.BootstrapTenantId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedTenantCannotBeModified);

        var retained = await ReloadTenantAsync(TestTenants.BootstrapTenantId);

        retained.IsDeleted.Should().BeFalse("the refusal happened before anything was written");
        retained.SystemCreated.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that deleting a tenant that is already deleted is refused as a tenant that is not there,
    /// which is the same answer an unknown tenant gets and so reveals nothing about the deletion
    /// (AC-009).
    /// </summary>
    [Fact]
    public async Task Deleting_An_Already_Deleted_Tenant_Is_Not_Found()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (firstResponse, _) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = tenant.Id });

        var (secondResponse, problem) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, ProblemDetails>(new() { Id = tenant.Id });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        var retained = await ReloadRetainedTenantAsync(tenant.Id);

        retained.DeletedAt.Should().NotBeNull("the second attempt does not rewrite the deletion that already happened");
    }

    /// <summary>
    /// Verifies that a tenant that has never existed is refused, and that a request naming no tenant at
    /// all is refused before anything is read - the remaining failure branches of this endpoint
    /// (AC-086).
    /// </summary>
    [Fact]
    public async Task Unknown_And_Missing_Tenant_Are_Rejected()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (unknownResponse, unknownProblem) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, ProblemDetails>(new() { Id = Guid.NewGuid() });

        var (missingResponse, missingProblem) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, ProblemDetails>(new() { Id = Guid.Empty });

        unknownResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknownProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantNotFound);

        missingResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingProblem.Errors.Should().Contain(error => error.Name == "id", "an empty id never reaches a handler");
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
}
