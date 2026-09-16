namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="FileGetEndpoint"/>: that a file attributed to another tenant is refused with the
/// code that names why (AC-058), that knowing the stored name buys nothing (AC-059), that a file
/// uploaded in a tenant stops being served once that tenant is deleted while being retained (AC-060), and
/// that an unauthenticated caller is refused before any of that (AC-098).
/// </summary>
/// <remarks>
/// Each refusal is read twice, once as the response the typed helper deserializes and once as the raw
/// bytes the endpoint actually sent, because the criterion is about both: a defined error code, and a
/// body that carries no part of the file. A refusal that streamed the content under an error status
/// would satisfy the first and fail the second.
/// </remarks>
public class FileGetTests(App app) : FileTestsBase(app)
{
    /// <summary>
    /// Verifies that a caller acting in one tenant is refused a file attributed to another, with the code
    /// that names the refusal, the file left intact, and its own tenant still served (AC-058).
    /// </summary>
    [Fact]
    public async Task Cross_Tenant_File_Is_Refused()
    {
        var owner = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var ownerMember = await CreateTenantUserAsync(owner.Id);
        var stranger = await CreateTenantUserAsync(other.Id);

        const string content = "notes belonging to the first tenant";

        var ownerClient = await ClientForAsync(ownerMember.Username, owner.Id);
        var storedName = await UploadAsync(ownerClient, content);

        var strangerClient = await ClientForAsync(stranger.Username, other.Id);

        var (response, refusal) = await strangerClient
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = storedName });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the file exists and belongs elsewhere, so the read is refused rather than answered");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.CrossTenantFileAccess,
            "the code names the attribution as the reason, so the refusal discloses nothing about the file beyond its not being this caller's");

        var raw = await RequestContentAsync(strangerClient, storedName);

        raw.StatusCode.Should().Be(response.StatusCode, "the same request answers the same way however it is read");
        (await raw.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(content,
            "the refusal is an error description, so not one byte of the file reaches a caller it does not belong to");

        // Refused, not consumed: the owner goes on being served the very file the other tenant was
        // refused, which is what makes this a refusal of access rather than of the file's existence.
        var ownerRead = await RequestContentAsync(ownerClient, storedName);

        ownerRead.StatusCode.Should().Be(HttpStatusCode.OK, "the tenant the file belongs to is still served it");
        (await ownerRead.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(content);
    }

    /// <summary>
    /// Verifies that the stored name itself, read from the record that attributes the file, yields no part
    /// of another tenant's content (AC-059).
    /// </summary>
    /// <remarks>
    /// The name is taken from the record rather than from the upload response, because that is the value a
    /// stranger would have to come by for the guess to be a real one. A name that matches no record at all
    /// is asked for alongside it: that one is answered as missing, so the refusal of the real name is shown
    /// to be about the file's attribution rather than about this caller being refused every address.
    /// </remarks>
    [Fact]
    public async Task Guessed_Stored_Name_Does_Not_Yield_Another_Tenants_Content()
    {
        var owner = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var ownerMember = await CreateTenantUserAsync(owner.Id);
        var stranger = await CreateTenantUserAsync(other.Id);

        const string content = "notes a guessed name must not yield";

        var ownerClient = await ClientForAsync(ownerMember.Username, owner.Id);
        var uploadedName = await UploadAsync(ownerClient, content);

        var storedName = (await StoredFileAsync(uploadedName)).FileName;

        var strangerClient = await ClientForAsync(stranger.Username, other.Id);

        var (response, refusal) = await strangerClient
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = storedName });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.First().Code.Should().Be(ErrorCodes.CrossTenantFileAccess,
            "knowing the stored name changes nothing, because the name is an address and the record is the permission");

        var raw = await RequestContentAsync(strangerClient, storedName);

        (await raw.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(content,
            "zero content bytes reach the caller: the address was right and it bought nothing");

        var unknown = await RequestContentAsync(strangerClient, $"{Guid.NewGuid():N}.txt");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a name no record matches is answered as missing, so the refusal above is about the file rather than about the caller");
    }

    /// <summary>
    /// Verifies that a file uploaded in a tenant stops being served once that tenant is deleted, while
    /// the record attributing it and the stored bytes are retained - so deletion withdraws access without
    /// destroying data (AC-060).
    /// </summary>
    /// <remarks>
    /// The file is served to the same client before the deletion, so the refusal that follows is provably
    /// the deletion's doing rather than a read that was never admissible. The refusal is stated over the
    /// file's tenant rather than over the caller's session: this endpoint is exempt from the global tenant
    /// requirement - an account-owned file has to be readable while the owner acts in any tenant or in none
    /// - so the request reaches the file's own attribution, which reports that there is no live tenant left
    /// to serve it in. That is a different verdict from the session-level refusal the tenant-scoped
    /// endpoints answer with, and it is the one the criterion is about, because it is reached by looking
    /// the file up rather than by refusing the caller before any file is considered.
    /// </remarks>
    [Fact]
    public async Task Deleted_Tenant_Files_Are_Not_Served_But_Are_Retained()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var memberClient = await ClientForAsync(member.Username, tenant.Id);

        const string content = "the deleted tenant's own file";
        var storedName = await UploadAsync(memberClient, content);

        var served = await RequestContentAsync(memberClient, storedName);

        served.StatusCode.Should().Be(HttpStatusCode.OK, "the tenant is in service and the file belongs to it");
        (await served.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(content);

        await SetAuthTokenAsync();

        var (deleteResponse, _) = await App.Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = tenant.Id });

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK, "a test that cannot delete the tenant cannot arrange what it asserts on");

        var (refused, refusal) = await memberClient
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = storedName });

        refused.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "the file is looked up and found to have no live tenant to be served in, so the read is refused rather than answered");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(
            ErrorCodes.TenantNotFound,
            "the refusal names the state the file's tenant is in, which is a verdict about the file rather than about the caller's session");

        var raw = await RequestContentAsync(memberClient, storedName);

        raw.StatusCode.Should().Be(refused.StatusCode, "the same request answers the same way however it is read");
        (await raw.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(
            content,
            "not one byte of the file reaches a caller whose tenant is gone");

        // Retained, not destroyed: the record still attributes the file to the tenant, and the bytes are
        // still in storage, so a deletion that was undone would find the tenant's files where it left them.
        var stored = await StoredFileAsync(storedName);

        stored.TenantId.Should().Be(tenant.Id, "the file stays attributed to the tenant it was uploaded in");
        stored.OriginalFileName.Should().Be(NotesFileName);
        StorageProvider.Exists(storedName).Should().BeTrue("the content is retained while the tenant is out of service");
    }

    /// <summary>
    /// Verifies that an unauthenticated read is refused with the standard unauthenticated response
    /// (AC-098).
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var (response, _) = await App.Client
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = $"{Guid.NewGuid():N}.txt" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the endpoint is no longer anonymously reachable, so no stored file is even looked for");
    }
}
