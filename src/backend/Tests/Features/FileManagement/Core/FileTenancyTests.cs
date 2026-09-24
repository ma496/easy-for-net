namespace Backend.Tests.Features.FileManagement.Core;

using Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// The combined file proof: that a file belonging to an account rather than to a tenant's data follows
/// the account across every tenant and into none, and that a file belonging to one tenant is
/// neither read, replaced nor removed by a member of another, however precisely the stored name is known
///.
/// </summary>
/// <remarks>
/// <para>
/// The two criteria are one proof read from both ends. A tenant-scoped file and an account-owned one are
/// attributed through the same record and refused by the same read of it, so the value of testing them
/// together is that the difference in outcome is shown to come from the attribution and from nothing
/// else: the same caller, the same stored name, the same endpoint.
/// </para>
/// <para>
/// Replacing a stored file is a deletion followed by an upload - there is no operation that swaps one for
/// another - so the step of a replacement that can be refused is the deletion, and it is the deletion that
/// is refused here. The upload that would follow it can only ever store a new file belonging to the
/// caller's own tenant.
/// </para>
/// </remarks>
public class FileTenancyTests(App app) : FileTestsBase(app)
{
    /// <summary>
    /// Verifies that an account-owned file is attributed to no tenant and to its account, and that the
    /// account reads it while acting in either of its tenants and while acting in none.
    /// </summary>
    /// <remarks>
    /// The account holds two memberships, which is what lets it act in either tenant, and belongs to the
    /// platform tier, which is what lets it sign in acting in none at all - an ordinary account cannot,
    /// since it would then hold no permission whatever. The tier is a way of reaching the third standing
    /// and nothing more: what is under test is where the file is attributed, which the tier does not
    /// touch. The content is read back in each standing, because "attributed to no tenant" would
    /// otherwise be indistinguishable from a file nobody can open.
    /// </remarks>
    [Fact]
    public async Task Account_Owned_File_Is_Readable_In_Any_Tenant_And_In_None()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var owner = await CreateDualTenantMemberAsync(first.Id, second.Id);
        await MarkAsPlatformAccountAsync(owner.Id);

        const string content = "the image this account uses everywhere";

        var uploaderClient = await ClientForAsync(owner.Username, first.Id);

        var (uploadResponse, uploaded) = await uploaderClient
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(
                UploadRequest(content, "avatar.png", "image/png", accountOwned: true),
                sendAsFormData: true);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK, "an account-owned upload is answered while the caller acts in a tenant");
        uploaded.FileName.Should().NotBeNullOrWhiteSpace();

        var stored = await StoredFileAsync(uploaded.FileName);

        stored.TenantId.Should().BeNull(
            "the file belongs to the account rather than to the tenant it happened to be uploaded in, which is what keeps it from becoming that tenant's data");
        stored.OwnerUserId.Should().Be(owner.Id, "and it names the account it belongs to, which is the only thing that may read it");

        var whileActingInFirst = await ClientForAsync(owner.Username, first.Id);
        var whileActingInSecond = await ClientForAsync(owner.Username, second.Id);
        var whileActingInNone = await ClientForAsync(owner.Username);

        var inFirst = await RequestContentAsync(whileActingInFirst, uploaded.FileName);
        var inSecond = await RequestContentAsync(whileActingInSecond, uploaded.FileName);
        var inNone = await RequestContentAsync(whileActingInNone, uploaded.FileName);

        var token = TestContext.Current.CancellationToken;

        inFirst.StatusCode.Should().Be(HttpStatusCode.OK, "the owner reads their own file while acting in the tenant it was uploaded in");
        inSecond.StatusCode.Should().Be(HttpStatusCode.OK, "and while acting in another tenant, since the file belongs to no tenant of its own");
        inNone.StatusCode.Should().Be(HttpStatusCode.OK, "and while acting in no tenant at all, which is the standing a profile image is most often needed in");

        (await inFirst.Content.ReadAsStringAsync(token)).Should().Be(content);
        (await inSecond.Content.ReadAsStringAsync(token)).Should().Be(content);
        (await inNone.Content.ReadAsStringAsync(token)).Should().Be(content);
    }

    /// <summary>
    /// Verifies that a member of one tenant can neither read, replace nor delete a file belonging to
    /// another, including when the stored name is supplied verbatim, while an account-owned image stays
    /// readable in every standing its owner acts in.
    /// </summary>
    /// <remarks>
    /// The member of the other tenant holds the delete permission in their own tenant, so each refusal is
    /// the file's attribution rather than their authority over the endpoint: without that, authorization
    /// would refuse them before any file was considered and the test would prove nothing about tenants.
    /// Every refusal is followed by a read as the owning tenant, so the file is shown to have survived
    /// rather than merely to have been refused. The account-owned half closes the proof from the other
    /// side: the same endpoint, the same kind of name, answered with the content when the caller owns it.
    /// </remarks>
    [Fact]
    public async Task Member_Of_One_Tenant_Cannot_Read_Replace_Or_Delete_Another_Tenants_File()
    {
        var ownerTenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var ownerMember = await CreateTenantUserAsync(ownerTenant.Id);
        var otherRoleId = await CreateTenantRoleAsync(otherTenant.Id, Allow.File_Delete);
        var stranger = await CreateTenantUserAsync(otherTenant.Id, otherRoleId);

        const string content = "notes one tenant's data holds and another's must not";

        var ownerClient = await ClientForAsync(ownerMember.Username, ownerTenant.Id);
        var uploadedName = await UploadAsync(ownerClient, content);

        // The name as the record holds it: a stranger who has come by it has the address, which is
        // exactly what the criterion says must buy them nothing.
        var storedName = (await StoredFileAsync(uploadedName)).FileName;

        var strangerClient = await ClientForAsync(stranger.Username, otherTenant.Id);

        var (readResponse, readRefusal) = await strangerClient
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = storedName });

        readResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the file belongs to another tenant, so the read is refused");
        readRefusal.Errors.First().Code.Should().Be(ErrorCodes.CrossTenantFileAccess);

        var (deleteResponse, deleteRefusal) = await strangerClient
            .DELETEAsync<FileDeleteEndpoint, FileDeleteRequest, ProblemDetails>(new() { FileName = storedName });

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the deletion a replacement begins with is refused the same way the read is");
        deleteRefusal.Errors.First().Code.Should().Be(ErrorCodes.CrossTenantFileAccess);

        var refusedBytes = await RequestContentAsync(strangerClient, storedName);

        (await refusedBytes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(content,
            "neither refusal handed over a byte, so supplying the name verbatim bought nothing");

        (await StoredFileExistsAsync(storedName)).Should().BeTrue("the file the other tenant could not remove is still attributed to its tenant");
        StorageProvider.Exists(storedName).Should().BeTrue("and its content is still in storage, since a refused removal removes nothing");

        var ownerRead = await RequestContentAsync(ownerClient, storedName);

        ownerRead.StatusCode.Should().Be(HttpStatusCode.OK, "the owning tenant is still served the file the other was refused");
        (await ownerRead.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(content);

        // The account-owned image, read in both standings its owner acts in: the endpoint
        // that refuses the other tenant's file answers this caller with the content, so what decided the
        // refusals above was the attribution written at upload and not the file name or the endpoint.
        // The platform tier is how this account reaches the third standing - acting in no tenant at all,
        // which an ordinary account cannot sign in to - and touches nothing else the case asserts.
        var imageOwner = await CreateDualTenantMemberAsync(ownerTenant.Id, otherTenant.Id);
        await MarkAsPlatformAccountAsync(imageOwner.Id);

        var imageClient = await ClientForAsync(imageOwner.Username, ownerTenant.Id);
        var imageName = await UploadAsync(imageClient, "the image this account owns", "avatar.png", accountOwned: true);

        var imageInOwnTenant = await RequestContentAsync(imageClient, imageName);
        var imageElsewhere = await RequestContentAsync(await ClientForAsync(imageOwner.Username, otherTenant.Id), imageName);
        var imageWithNoTenant = await RequestContentAsync(await ClientForAsync(imageOwner.Username), imageName);

        imageInOwnTenant.StatusCode.Should().Be(HttpStatusCode.OK);
        imageElsewhere.StatusCode.Should().Be(HttpStatusCode.OK);
        imageWithNoTenant.StatusCode.Should().Be(HttpStatusCode.OK);

        (await imageWithNoTenant.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be("the image this account owns", "an account-owned file is served to its owner in every standing, which is what the tenant-scoped file above could not be");
    }
}
