namespace Backend.Tests.Features.FileManagement;

using System.Text;
using Backend.Features.FileManagement.Core;
using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Tests.Features.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Base class for the file endpoint tests: what an upload of a small in-memory file looks like, and the
/// two places its outcome is read back from - the <see cref="StoredFile"/> row that attributes it, and
/// the bytes the storage provider actually holds.
/// </summary>
/// <remarks>
/// <para>
/// A stored file name is an address and never a permission, so every read and delete in these tests is
/// judged against the record rather than against the name: the row is read with
/// <see cref="TenantQueryExtension.AcrossAllTenants{T}"/> because what is asked of it - which tenant it
/// belongs to, whether it still exists at all - is a question about the row rather than about what a
/// caller acting somewhere may reach. Reading it through the tenant filter would make another tenant's
/// file indistinguishable from a missing one, which is exactly the distinction under test.
/// </para>
/// <para>
/// The bytes are read through <see cref="IStorageProvider"/> for the same reason: a refusal that
/// destroyed the content would satisfy "the caller did not get it" while breaking the retention AC-060
/// requires, so what the storage holds is asserted alongside what the caller was answered.
/// </para>
/// <para>
/// Every tenant, role, membership, account and file a test asserts on is made by the test itself. The
/// suite runs its collections sequentially against one shared database and one shared storage
/// directory, so a test that weighed its own upload against rows or bytes it did not create would be
/// asserting on whatever the rest of the suite has left behind.
/// </para>
/// </remarks>
// One collection for the whole file suite. Storage is a single uploads directory under the host's
// content root, and a test that weighs what the directory holds before and against after cannot
// have another test writing into it meanwhile.
[Collection("FileManagement")]
public abstract class FileTestsBase(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The original filename the uploads use unless a test needs one of its own. The stored name is
    /// generated, so this is what the record reports back rather than what the file is called on disk.
    /// </summary>
    protected const string NotesFileName = "notes.txt";

    /// <summary>
    /// The content type the uploads are made with, chosen so that a refusal answered with problem
    /// details is told apart from the file itself being streamed back.
    /// </summary>
    protected const string NotesContentType = "text/plain";

    /// <summary>
    /// The storage provider the running host writes through, for asking whether a file's content is
    /// still there - which is not the same question as whether the caller was allowed to have it.
    /// </summary>
    protected IStorageProvider StorageProvider => Service<IStorageProvider>();

    /// <summary>
    /// Builds a multipart upload of the given text as a small in-memory file.
    /// </summary>
    /// <param name="content">The text the file is to hold.</param>
    /// <param name="fileName">The original filename to submit.</param>
    /// <param name="contentType">The content type to submit.</param>
    /// <param name="accountOwned">Whether the upload is to belong to the calling account rather than to a tenant.</param>
    /// <returns>The request, ready to be posted as form data.</returns>
    /// <remarks>
    /// The stream behind the form part is a <see cref="MemoryStream"/> over the bytes and is deliberately
    /// left to the garbage collector: it holds no handle of its own, and the request carries it for the
    /// duration of the call.
    /// </remarks>
    protected static FileUploadRequest UploadRequest(string content,
                                                     string fileName = NotesFileName,
                                                     string contentType = NotesContentType,
                                                     bool accountOwned = false)
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        return new FileUploadRequest
        {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            },
            AccountOwned = accountOwned
        };
    }

    /// <summary>
    /// Uploads the given text as a file and returns the stored name the endpoint answered with.
    /// </summary>
    /// <param name="client">The client to upload as, and the tenant it is acting in.</param>
    /// <param name="content">The text the file is to hold.</param>
    /// <param name="fileName">The original filename to submit.</param>
    /// <param name="accountOwned">Whether the upload is to belong to the calling account rather than to a tenant.</param>
    /// <returns>The stored name of the uploaded file.</returns>
    protected static async Task<string> UploadAsync(HttpClient client,
                                                    string content,
                                                    string fileName = NotesFileName,
                                                    bool accountOwned = false)
    {
        var (response, uploaded) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(
                UploadRequest(content, fileName, accountOwned: accountOwned),
                sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test that cannot upload cannot arrange what it asserts on");

        return uploaded.FileName;
    }

    /// <summary>
    /// Reads the record attributing a stored file, across every tenant.
    /// </summary>
    /// <param name="fileName">The stored file name the record is found by.</param>
    /// <returns>The record, as the database holds it.</returns>
    protected async Task<StoredFile> StoredFileAsync(string fileName)
        => await DbContext.StoredFiles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(file => file.FileName == fileName, TestContext.Current.CancellationToken);

    /// <summary>
    /// Whether any record at all attributes a file submitted under that original filename - which is the
    /// only name a refused upload could have left behind, its stored name never having been generated.
    /// </summary>
    /// <param name="originalFileName">The original filename an upload would have been submitted under.</param>
    /// <returns><see langword="true"/> when a record names that original filename.</returns>
    protected async Task<bool> AnyStoredFileNamedAsync(string originalFileName)
        => await DbContext.StoredFiles
            .AcrossAllTenants()
            .AnyAsync(file => file.OriginalFileName == originalFileName, TestContext.Current.CancellationToken);

    /// <summary>
    /// Whether the record attributing a stored file is still there at all.
    /// </summary>
    /// <param name="fileName">The stored file name the record is found by.</param>
    /// <returns><see langword="true"/> when the record still exists.</returns>
    protected async Task<bool> StoredFileExistsAsync(string fileName)
        => await DbContext.StoredFiles
            .AcrossAllTenants()
            .AnyAsync(file => file.FileName == fileName, TestContext.Current.CancellationToken);

    /// <summary>
    /// The names of the files the storage directory holds right now.
    /// </summary>
    /// <returns>The stored names, in whatever order the filesystem reports them.</returns>
    /// <remarks>
    /// An upload's stored name is generated, so a refused one leaves no name to look for and the
    /// directory is weighed before and after it instead: what is asserted is that the refusal left
    /// storage exactly as it found it.
    /// </remarks>
    protected List<string> StoredContentNames()
    {
        var storageDirectory = Path.Combine(
            Service<IWebHostEnvironment>().ContentRootPath,
            "uploads");

        if (!Directory.Exists(storageDirectory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(storageDirectory).Select(Path.GetFileName).OfType<string>()];
    }

    /// <summary>
    /// Reads a stored file's content through the endpoint that streams it.
    /// </summary>
    /// <param name="client">The client to read as, and the tenant it is acting in.</param>
    /// <param name="fileName">The stored file name to read.</param>
    /// <returns>The response, whose body is the file itself when the read was granted.</returns>
    /// <remarks>
    /// Called directly rather than through the typed helper because a granted read answers with the
    /// file's own bytes and content type rather than with JSON, and a refused one has to be read as the
    /// bytes it actually is: what is asserted of both is the body, which no deserializer would produce.
    /// </remarks>
    protected static Task<HttpResponseMessage> RequestContentAsync(HttpClient client, string fileName)
        => client.GetAsync(FileUrl(fileName), TestContext.Current.CancellationToken);

    /// <summary>
    /// Deletes a stored file through the endpoint that removes it.
    /// </summary>
    /// <param name="client">The client to delete as, and the tenant it is acting in.</param>
    /// <param name="fileName">The stored file name to remove.</param>
    /// <returns>The response, which carries no content whether it granted or refused.</returns>
    /// <remarks>
    /// Called directly because a granted removal answers with no content at all, so there is no response
    /// type for a typed helper to deserialize; a refused one is read through the typed helper, its body
    /// being the error that names the reason.
    /// </remarks>
    protected static Task<HttpResponseMessage> RequestDeleteAsync(HttpClient client, string fileName)
        => client.DeleteAsync(FileUrl(fileName), TestContext.Current.CancellationToken);

    /// <summary>
    /// The address of a stored file, built the way the running host mounts the file group under its
    /// configured route prefix. Used only where a response has to be read as raw bytes.
    /// </summary>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The URL to call.</returns>
    private static string FileUrl(string fileName) => $"/api/file-management/{Uri.EscapeDataString(fileName)}";
}
