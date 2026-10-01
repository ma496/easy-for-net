namespace EasyForNetTool.Tests.Generator;

using System.Text.Json.Nodes;
using EasyForNetTool.Generator;

/// <summary>
/// Unit tests for <see cref="CreateProjectGenerator.KeepOfflineResourceLocalesAsync"/>.
/// </summary>
public class KeepOfflineResourceLocalesTests
{
    private const string Template = """
        {
          "en": { "brand.name": "Demo App", "error.serviceUnavailable.retry": "Retry" },
          "ur": { "brand.name": "Demo App", "error.serviceUnavailable.retry": "دوبارہ کوشش کریں" },
          "fr": { "brand.name": "Demo App", "error.serviceUnavailable.retry": "Réessayer" }
        }
        """;

    /// <summary>
    /// Tests that only the kept cultures remain, with their strings unchanged.
    /// </summary>
    [Fact]
    public async Task Should_Remove_Every_Culture_But_The_Kept_Ones()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "efn-offline-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, Template);
        try
        {
            // Act
            await CreateProjectGenerator.KeepOfflineResourceLocalesAsync(path, ["en"]);

            // Assert
            var resources = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            Assert.Equal(["en"], resources.Select(entry => entry.Key));
            Assert.Equal("Demo App", resources["en"]!["brand.name"]!.GetValue<string>());
            Assert.Equal("Retry", resources["en"]!["error.serviceUnavailable.retry"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Tests that a project without the file is left alone rather than failing generation.
    /// </summary>
    [Fact]
    public async Task Should_Do_Nothing_When_The_File_Is_Missing()
    {
        var path = Path.Combine(Path.GetTempPath(), "efn-offline-" + Guid.NewGuid().ToString("N") + ".json");

        await CreateProjectGenerator.KeepOfflineResourceLocalesAsync(path, ["en"]);

        Assert.False(File.Exists(path));
    }
}
