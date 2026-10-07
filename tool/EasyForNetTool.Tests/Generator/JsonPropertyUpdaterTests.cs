namespace EasyForNetTool.Tests.Generator;

using System.Text.Json;

/// <summary>
/// Unit tests for <see cref="JsonPropertyUpdater"/>.
/// </summary>
public class JsonPropertyUpdaterTests
{
    /// <summary>
    /// Tests that the values the update does not touch are written back as they were, with no
    /// <c>\uXXXX</c> escape for <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, <c>'</c>, <c>+</c> or non-ASCII text.
    /// </summary>
    [Fact]
    public async Task Should_Not_Escape_Untouched_Values()
    {
        // Arrange
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "package.json");
            await File.WriteAllTextAsync(path, "{\n  \"name\": \"x\",\n  \"engines\": { \"node\": \">=24 <25\" },\n  \"note\": \"a & b's — c+d\"\n}\n");

            // Act
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(path, "name", "demo-app");

            // Assert
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("\">=24 <25\"", text);
            Assert.Contains("\"a & b's — c+d\"", text);
            Assert.DoesNotContain("\\u", text);
            using var doc = JsonDocument.Parse(text);
            Assert.Equal("demo-app", doc.RootElement.GetProperty("name").GetString());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that the rewrite writes LF line endings and keeps the file's trailing newline.
    /// </summary>
    [Fact]
    public async Task Should_Write_Lf_And_Keep_Trailing_Newline()
    {
        // Arrange
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "package.json");
            await File.WriteAllTextAsync(path, "{\n  \"name\": \"x\",\n  \"private\": true\n}\n");

            // Act
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(path, "name", "demo-app");

            // Assert
            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("\r\n", text);
            Assert.EndsWith("}\n", text);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests the rewrite against the template's own JSON files the generator renames a project in, so no
    /// value in them comes out escaped in a generated project.
    /// </summary>
    [Theory]
    [InlineData("name", "package.json")]
    [InlineData("name", "src", "frontend", "web", "package.json")]
    [InlineData("name", "src", "frontend", "web", "package-lock.json")]
    [InlineData("project.name", "agentic.config.json")]
    [InlineData("Redis.InstanceName", "src", "backend", "Source", "appsettings.json")]
    public async Task Should_Not_Escape_Template_Files(string property, params string[] relativePath)
    {
        // Arrange
        var source = CustomizeAppSettingsTests.FindTemplateFile(relativePath);
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, Path.GetFileName(source));
            File.Copy(source, path);
            var escapesBefore = CountEscapes(await File.ReadAllTextAsync(path));

            // Act
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(path, property, "demo-app");

            // Assert
            Assert.Equal(escapesBefore, CountEscapes(await File.ReadAllTextAsync(path)));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static int CountEscapes(string text) => text.Split("\\u").Length - 1;

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "efn-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
