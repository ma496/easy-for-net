namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// Tests for <see cref="FileDeleteEndpoint"/>: that replacing or removing a file of another tenant is
/// refused with the code that names why and leaves the file exactly as it was, that removal in
/// the owning tenant takes both the record and the content, and that an unauthenticated caller is refused
///.
/// </summary>
/// <remarks>
/// A caller acting in the wrong tenant is given the delete permission itself, so what refuses them is the
/// file's attribution rather than their authority over the endpoint: without that, the refusal would be
/// produced by authorization before any file was ever considered and the test would prove nothing about
/// the tenant. Replacing a file is a deletion followed by an upload, so what holds for removal holds for
/// replacement.
/// </remarks>
public class FileDeleteTests(App app) : FileTestsBase(app)
{
    /// <summary>
    /// Verifies that a file belonging to the tenant the caller is acting in is removed, record and content
    /// together.
    /// </summary>
    /// <remarks>
    /// Both halves of the removal are asserted: a record left behind points at nothing, and content left
    /// behind is unreachable bytes nothing would ever tidy away.
    /// </remarks>
    [Fact]
    public async Task Delete_Removes_The_Record_And_The_Content()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.File_Delete);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var storedName = await UploadAsync(client, "notes the tenant is about to remove");

        StorageProvider.Exists(storedName).Should().BeTrue("the content reached storage before the removal");

        var response = await RequestDeleteAsync(client, storedName);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "the member holds the delete permission in the tenant that owns the file");

        (await StoredFileExistsAsync(storedName)).Should().BeFalse("the record granting access to the file is gone with it");
        StorageProvider.Exists(storedName).Should().BeFalse("and the content is gone, so nothing is left pointing at nothing");
    }

    /// <summary>
    /// Verifies that a member of another tenant cannot delete a file attributed to a tenant they are not
    /// acting in, and that the refusal leaves the file untouched.
    /// </summary>
    /// <remarks>
    /// The refuser holds the delete permission in their own tenant, so the refusal is the file's
    /// attribution and not their authority. The file is then read back by the tenant that owns it, which
    /// is what shows the refused deletion was refused rather than merely reported as refused.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_Delete_Is_Refused()
    {
        var owner = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var ownerMember = await CreateTenantUserAsync(owner.Id);
        var otherRoleId = await CreateTenantRoleAsync(other.Id, Allow.File_Delete);
        var stranger = await CreateTenantUserAsync(other.Id, otherRoleId);

        const string content = "notes the other tenant must not be able to remove";

        var ownerClient = await ClientForAsync(ownerMember.Username, owner.Id);
        var storedName = await UploadAsync(ownerClient, content);

        var strangerClient = await ClientForAsync(stranger.Username, other.Id);

        var (response, refusal) = await strangerClient
            .DELETEAsync<FileDeleteEndpoint, FileDeleteRequest, ProblemDetails>(new() { FileName = storedName });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the file exists and belongs elsewhere, so the removal is refused rather than reported as done");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.CrossTenantFileAccess,
            "the refusal is the same one a read gets, so a known stored name buys nothing here either");

        (await StoredFileExistsAsync(storedName)).Should().BeTrue("a refused removal leaves the record where it was");
        StorageProvider.Exists(storedName).Should().BeTrue("and the content with it, since a refused deletion removes nothing");

        var ownerRead = await RequestContentAsync(ownerClient, storedName);

        ownerRead.StatusCode.Should().Be(HttpStatusCode.OK, "the file the other tenant could not remove is still the owning tenant's to read");
        (await ownerRead.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(content);
    }

    /// <summary>
    /// Verifies that an unauthenticated removal is refused with the standard unauthenticated response
    ///.
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var response = await RequestDeleteAsync(Client, $"{Guid.NewGuid():N}.txt");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an anonymous caller has no tenant to be judged against, so no file is even looked for");
    }
}
