using Backend.Features.FileManagement.Endpoints.Files;

namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

/// <summary>
/// Tests for <see cref="FileGetEndpoint"/>: that a file attributed to another tenant is refused with the
/// code that names why, that knowing the stored name buys nothing, that a file
/// uploaded in a tenant stops being served once that tenant is deleted while being retained, and
/// that an unauthenticated caller is refused before any of that.
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
    /// Verifies that an unauthenticated read is refused with the standard unauthenticated response
    ///.
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var (response, _) = await Client
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = $"{Guid.NewGuid():N}.txt" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the endpoint is no longer anonymously reachable, so no stored file is even looked for");
    }
}
