using System.Text.Json.Nodes;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Seo.Endpoints.Seo;
using Microsoft.AspNetCore.Http;

namespace Backend.Tests.Features.Seo;

/// <summary>
/// Tests for <see cref="SeoImageEndpoint"/>: a stored file is served to anyone only while a page or the
/// <c>Seo</c> setting names it as a preview image, and is otherwise answered as missing.
/// </summary>
public class SeoImageTests(App app) : SeoTestsBase(app)
{
    [Fact]
    public async Task Serves_An_Image_A_Page_Names()
    {
        var fileId = await UploadAsync();
        await CreatePageAsync(new() { Path = NewPath(), OgImageFileId = fileId });

        var response = await AnonymousClient().GetAsync($"api/seo/images/{fileId:D}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.Should().BeTrue();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("image bytes");
    }

    [Fact]
    public async Task Serves_The_Default_Image()
    {
        var fileId = await UploadAsync();
        await SetSeoAsync(new JsonObject { ["defaultOgImageFileId"] = fileId.ToString() });

        var response = await AnonymousClient().GetAsync($"api/seo/images/{fileId:D}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refuses_A_Stored_File_No_Page_Names()
    {
        var fileId = await UploadAsync();

        var response = await AnonymousClient().GetAsync($"api/seo/images/{fileId:D}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "the endpoint is no way to read the application's files");
    }

    [Fact]
    public async Task Refuses_An_Image_Whose_Page_Was_Deleted()
    {
        var fileId = await UploadAsync();
        var page = await CreatePageAsync(new() { Path = NewPath(), OgImageFileId = fileId });
        var admin = await AdminClientAsync();
        (await admin.DELETEAsync<Backend.Features.Seo.Endpoints.Pages.SeoPageDeleteEndpoint, Backend.Features.Seo.Endpoints.Pages.SeoPageDeleteRequest, Backend.Features.Seo.Endpoints.Pages.SeoPageDeleteResponse>(
                new() { Id = page.Id }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await AnonymousClient().GetAsync($"api/seo/images/{fileId:D}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Answers_404_For_A_File_That_Does_Not_Exist_Even_When_Named()
    {
        var fileId = Guid.NewGuid();
        await CreatePageAsync(new() { Path = NewPath(), OgImageFileId = fileId });

        var response = await AnonymousClient().GetAsync($"api/seo/images/{fileId:D}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Uploads an application file as the administrator and returns its identifier.</summary>
    private async Task<Guid> UploadAsync()
    {
        var bytes = "image bytes"u8.ToArray();
        var request = new FileUploadRequest
        {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "preview.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            }
        };

        var admin = await AdminClientAsync();
        var (response, uploaded) = await admin.POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(request, sendAsFormData: true);
        DeleteAfterTest(uploaded);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        uploaded.Id.Should().NotBeEmpty();
        return uploaded.Id;
    }
}
