namespace EasyForNetTool.Tests.Generator;

using System.Text.Json.Nodes;
using EasyForNetTool.Generator;

/// <summary>
/// Unit tests for <see cref="CreateProjectGenerator.RemoveExampleResourceKeysAsync"/>.
/// </summary>
public class RemoveExampleResourceKeysTests : IDisposable
{
    private const string Resources = """
        {
          "navigation": {
            "profile": "Profile",
            "components": "Components",
            "uiButtons": "Buttons",
            "ui": "kept: no capital after ui"
          },
          "search": {
            "uiTooltips": "Tooltips",
            "tenants": "Tenants"
          },
          "page": {
            "ui": { "buttons": { "title": "Buttons" } },
            "notifications": { "title": "Notifications" }
          },
          "file": { "deleteTitle": "حذف" }
        }
        """;

    private readonly string _resourcesPath = Path.Combine(Path.GetTempPath(), "efn-resources-" + Guid.NewGuid().ToString("N"));

    public RemoveExampleResourceKeysTests() => Directory.CreateDirectory(_resourcesPath);

    public void Dispose() => Directory.Delete(_resourcesPath, true);

    /// <summary>
    /// Tests that the example-only keys are removed from every culture and their neighbours kept.
    /// </summary>
    [Fact]
    public async Task Should_Remove_Example_Keys_From_Every_Culture()
    {
        foreach (var culture in new[] { "en", "ar" })
            await File.WriteAllTextAsync(Path.Combine(_resourcesPath, $"{culture}.json"), Resources);

        await CreateProjectGenerator.RemoveExampleResourceKeysAsync(_resourcesPath);

        foreach (var culture in new[] { "en", "ar" })
        {
            var text = await File.ReadAllTextAsync(Path.Combine(_resourcesPath, $"{culture}.json"));
            var resources = JsonNode.Parse(text)!.AsObject();
            Assert.Equal(["profile", "ui"], resources["navigation"]!.AsObject().Select(entry => entry.Key));
            Assert.Equal(["tenants"], resources["search"]!.AsObject().Select(entry => entry.Key));
            Assert.Equal(["notifications"], resources["page"]!.AsObject().Select(entry => entry.Key));
            // non-ASCII text is written as it is, not as \uXXXX escapes
            Assert.Contains("\"deleteTitle\": \"حذف\"", text);
            Assert.EndsWith("}\n", text);
        }
    }

    /// <summary>
    /// Tests that a project without the resources directory is left alone rather than failing generation.
    /// </summary>
    [Fact]
    public async Task Should_Do_Nothing_When_The_Directory_Is_Missing()
    {
        var missing = Path.Combine(_resourcesPath, "missing");

        await CreateProjectGenerator.RemoveExampleResourceKeysAsync(missing);

        Assert.False(Directory.Exists(missing));
    }
}
