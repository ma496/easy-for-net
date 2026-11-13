using Backend.Features.FileManagement.Endpoints.Files;

namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

/// <summary>
/// Tests for <see cref="FileUploadEndpoint"/>: that an unauthenticated caller is refused before
/// anything is stored.
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
    /// Verifies that an unauthenticated upload is refused with the standard unauthenticated response.
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var (response, uploaded) = await Client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(UploadRequest("anonymous upload"), sendAsFormData: true);
        DeleteAfterTest(uploaded);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an anonymous caller has no account, so there would be nothing to attribute the file to");
    }
}
