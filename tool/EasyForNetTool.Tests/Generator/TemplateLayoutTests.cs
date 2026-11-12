using System.Text.Json;
using EasyForNetTool.Generator;

namespace EasyForNetTool.Tests.Generator;

/// <summary>
/// Unit tests for what differs per template in a generated project: the overlay copied over the
/// shared root files and the <c>apps</c> entry written to <c>agentic.config.json</c>.
/// </summary>
public class TemplateLayoutTests
{
    /// <summary>
    /// Tests that an overlay file replaces the shared file at the same path and a new one is added.
    /// </summary>
    [Fact]
    public async Task ApplyOverlay_Replaces_And_Adds_Files()
    {
        var dir = CreateTempDir();
        try
        {
            var template = Path.Combine(dir, "template");
            var target = Path.Combine(dir, "target");
            Directory.CreateDirectory(Path.Combine(template, "overlay", "docker"));
            Directory.CreateDirectory(Path.Combine(target, "docker"));
            await File.WriteAllTextAsync(Path.Combine(template, "overlay", "docker", "Caddyfile"), "overlay", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(template, "overlay", "extra.txt"), "added", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(target, "docker", "Caddyfile"), "shared", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(target, "kept.txt"), "kept", TestContext.Current.CancellationToken);

            CreateProjectGenerator.ApplyOverlay(template, target);

            Assert.Equal("overlay", await File.ReadAllTextAsync(Path.Combine(target, "docker", "Caddyfile"), TestContext.Current.CancellationToken));
            Assert.Equal("added", await File.ReadAllTextAsync(Path.Combine(target, "extra.txt"), TestContext.Current.CancellationToken));
            Assert.Equal("kept", await File.ReadAllTextAsync(Path.Combine(target, "kept.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that a template with no overlay leaves the project untouched.
    /// </summary>
    [Fact]
    public void ApplyOverlay_Without_An_Overlay_Does_Nothing()
    {
        var dir = CreateTempDir();
        try
        {
            CreateProjectGenerator.ApplyOverlay(Path.Combine(dir, "template"), dir);

            Assert.Empty(Directory.GetFileSystemEntries(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that the template repository's <c>apps</c> list becomes the one application of the
    /// generated layout, with the template's development hosts, and that the rest of the file survives.
    /// </summary>
    [Theory]
    [InlineData("multi-tenant", "app.localhost")]
    [InlineData("user-facing", "localhost")]
    public async Task WriteApps_Writes_The_Generated_Layout(string templateName, string firstHost)
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "agentic.config.json");
            File.Copy(CustomizeAppSettingsTests.FindTemplateFile("agentic.config.json"), path);

            await CreateProjectGenerator.WriteAppsAsync(path, ProjectTemplate.Find(templateName));

            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            using var doc = JsonDocument.Parse(text);
            var apps = doc.RootElement.GetProperty("apps");
            Assert.Equal(1, apps.GetArrayLength());
            Assert.Equal("src/backend", apps[0].GetProperty("backend").GetString());
            Assert.Equal("src/frontend/web", apps[0].GetProperty("web").GetString());
            Assert.Equal(firstHost, apps[0].GetProperty("devHosts")[0].GetString());
            Assert.True(doc.RootElement.TryGetProperty("departments", out _));
            var skills = doc.RootElement.GetProperty("skills").EnumerateArray().Select(s => s.GetProperty("skill").GetString()).ToList();
            Assert.Contains("backend-endpoint", skills);
            Assert.Empty(skills.Intersect(ProjectTemplate.Find(templateName).ExcludedSkills));
            Assert.DoesNotContain("\\u", text);
            Assert.EndsWith("}\n", text);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that the tool carries each template's CLAUDE.md and README.md, which the generator writes
    /// out by resource name: a missing one would fail only when a project is created.
    /// </summary>
    [Theory]
    [InlineData("multi-tenant")]
    [InlineData("user-facing")]
    public void Embedded_Files_Exist_For_Every_Template(string templateName)
    {
        var template = ProjectTemplate.Find(templateName);
        var assembly = typeof(CreateProjectGenerator).Assembly;
        var names = assembly.GetManifestResourceNames();

        Assert.Contains($"EasyForNetTool.{template.ClaudeResource}", names);
        Assert.Contains($"EasyForNetTool.{template.ReadmeResource}", names);
    }

    /// <summary>
    /// Tests that each template's example pages are where the generator deletes them from, so moving
    /// them without updating <see cref="ProjectTemplate.ExamplePagesPath"/> fails here rather than
    /// shipping them silently in every generated project.
    /// </summary>
    [Theory]
    [InlineData("multi-tenant")]
    [InlineData("user-facing")]
    public void ExamplePagesPath_Exists_In_The_Template(string templateName)
    {
        var template = ProjectTemplate.Find(templateName);
        var root = Path.GetDirectoryName(CustomizeAppSettingsTests.FindTemplateFile("agentic.config.json"))!;

        var path = Path.Combine([root, "src", template.Name, "frontend", "web", .. template.ExamplePagesPath]);

        Assert.True(Directory.Exists(path), $"{path} does not exist.");
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "efn-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
