namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// Tests for <see cref="FileUploadEndpoint"/>: that a tenant-scoped upload is attributed to the tenant
/// the caller is acting in (AC-057), that one reached with no tenant active is refused and stores
/// nothing (AC-099), and that an unauthenticated caller is refused before any of that (AC-098).
/// </summary>
/// <remarks>
/// Attribution is read off the record and off the file's reach rather than off the response, because the
/// response names only the stored file: what a caller supplied is an original filename and how the file
/// is attributed, and the first of those is not what decides the second. The upload that is refused is
/// weighed against both places it could have left something - a record, or bytes in storage - since a
/// refusal that stored either would be a refusal in name only.
/// </remarks>
public class FileUploadTests(App app) : FileTestsBase(app)
{
    /// <summary>
    /// Verifies that a tenant-scoped file is attributed to the tenant the uploading caller is acting in
    /// and read back from there (AC-057).
    /// </summary>
    /// <remarks>
    /// The file is read back in its own tenant as well as attributed, so "belongs to this tenant" is
    /// shown to mean the tenant can be served it rather than that a column was filled in.
    /// </remarks>
    [Fact]
    public async Task Upload_Is_Attributed_To_The_Active_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var client = await ClientForAsync(member.Username, tenant.Id);

        const string content = "the notes this tenant uploaded";

        var (response, uploaded) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(UploadRequest(content), sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a member acting in a tenant may store a file for it");
        uploaded.FileName.Should().NotBeNullOrWhiteSpace("the upload answers with the name the file was stored under");

        var stored = await StoredFileAsync(uploaded.FileName);

        stored.TenantId.Should().Be(tenant.Id,
            "a tenant-scoped file is attributed to the tenant active at the time of upload, and the caller supplied no tenant for it");
        stored.OwnerUserId.Should().BeNull(
            "it belongs to the tenant rather than to the account that happened to upload it, which is what makes it the tenant's data");
        stored.OriginalFileName.Should().Be(NotesFileName, "the name submitted is kept as the file's own name, the stored name being generated");

        var read = await RequestContentAsync(client, uploaded.FileName);

        read.StatusCode.Should().Be(HttpStatusCode.OK, "the tenant it was attributed to is the tenant it is served in");
        (await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(content);
    }

    /// <summary>
    /// Verifies that an upload of a tenant-scoped file reached with no active tenant is refused and
    /// stores neither a record nor any bytes (AC-099).
    /// </summary>
    /// <remarks>
    /// The uploader belongs to the platform tier, which is the standing that signs in acting in no
    /// tenant at all: there is then no tenant for a tenant-scoped file to belong to, and this endpoint
    /// says so rather than storing one that belongs nowhere. The refusal is weighed against the storage
    /// directory as well as against the records, because a stored file whose record was never written
    /// would be unreachable content that nothing would ever tidy away.
    /// </remarks>
    [Fact]
    public async Task Upload_Without_An_Active_Tenant_Is_Refused()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var unattached = await CreateDualTenantMemberAsync(first.Id, second.Id);
        await MarkAsPlatformAccountAsync(unattached.Id);
        var client = await ClientForAsync(unattached.Username);

        // A name no other run can submit, so "no record names it" is a statement about this upload
        // rather than about whether anything else happened to be called this.
        var submittedName = $"{Guid.NewGuid():N}.txt";

        var contentBefore = StoredContentNames();

        var (response, refusal) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, ProblemDetails>(
                UploadRequest("a file with no tenant to belong to", submittedName),
                sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the request is refused rather than answered with a stored name");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.NoActiveTenant,
            "the refusal names the missing tenant, so a caller can tell it from a file that was rejected for its own sake");

        (await AnyStoredFileNamedAsync(submittedName)).Should().BeFalse(
            "a refused upload records nothing, since a record naming no tenant would attribute the file to nobody");

        StoredContentNames().Should().BeEquivalentTo(contentBefore,
            "and the refusal is settled before the content is read, so storage holds exactly what it held before it");
    }

    /// <summary>
    /// Verifies that an unauthenticated upload is refused with the standard unauthenticated response
    /// (AC-098).
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var (response, _) = await App.Client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(UploadRequest("anonymous upload"), sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an anonymous caller has no tenant and no account, so there would be nothing to attribute the file to");
    }
}
