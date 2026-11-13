using Backend.Features.FileManagement.Endpoints.Files;

namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

/// <summary>
/// Tests for <see cref="FileDeleteEndpoint"/>: that removing a file takes both the record and the
/// content, and that an unauthenticated caller is refused before any file is looked for.
/// </summary>
/// <remarks>
/// Replacing a file is a deletion followed by an upload, so what holds for removal holds for
/// replacement.
/// </remarks>
public class FileDeleteTests(App app) : FileTestsBase(app)
{
    /// <summary>
    /// Verifies that a file the caller may delete is removed, record and content
    /// together.
    /// </summary>
    /// <remarks>
    /// Both halves of the removal are asserted: a record left behind points at nothing, and content left
    /// behind is unreachable bytes nothing would ever tidy away.
    /// </remarks>
    [Fact]
    public async Task Delete_Removes_The_Record_And_The_Content()
    {
        var roleId = await CreateRoleAsync(Allow.File_Delete);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var storedName = await UploadAsync(client, "notes about to be removed");

        StorageProvider.Exists(storedName).Should().BeTrue("the content reached storage before the removal");

        var response = await RequestDeleteAsync(client, storedName);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "the caller holds the delete permission");

        (await StoredFileExistsAsync(storedName)).Should().BeFalse("the record granting access to the file is gone with it");
        StorageProvider.Exists(storedName).Should().BeFalse("and the content is gone, so nothing is left pointing at nothing");
    }

    /// <summary>
    /// Verifies that an unauthenticated removal is refused with the standard unauthenticated response.
    /// </summary>
    [Fact]
    public async Task Unauthenticated()
    {
        ClearAuthToken();

        var response = await RequestDeleteAsync(Client, $"{Guid.NewGuid():N}.txt");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an anonymous caller is refused before any file is even looked for");
    }
}
