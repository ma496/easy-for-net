using EasyForNetTool.Generator;

namespace EasyForNetTool.Tests.Generator;

/// <summary>
/// Unit tests for <see cref="CreateProjectGenerator.StripExampleRegionsAsync"/>.
/// </summary>
public class StripExampleRegionsTests : IDisposable
{
    private const string NavItems = """
        import { Home } from 'lucide-react'
        // efn:examples:start
        import { Palette } from 'lucide-react'
        // efn:examples:end

        export const navItems = [
          { title: 'navigation.dashboard', icon: Home },
          // efn:examples:start
          { title: 'navigation.uiCards', icon: Palette },
          // efn:examples:end
        ]

        """;

    private readonly string _webPath = Path.Combine(Path.GetTempPath(), "efn-examples-" + Guid.NewGuid().ToString("N"));

    public StripExampleRegionsTests() => Directory.CreateDirectory(_webPath);

    public void Dispose() => Directory.Delete(_webPath, true);

    /// <summary>
    /// Tests that a region is removed together with its markers, and everything outside it is kept.
    /// </summary>
    [Fact]
    public async Task Should_Remove_Regions_With_Their_Markers()
    {
        var path = await WriteAsync("nav-items.ts", NavItems);

        await CreateProjectGenerator.StripExampleRegionsAsync(_webPath, keepExamples: false);

        Assert.Equal("""
            import { Home } from 'lucide-react'

            export const navItems = [
              { title: 'navigation.dashboard', icon: Home },
            ]

            """, await File.ReadAllTextAsync(path));
    }

    /// <summary>
    /// Tests that keeping the examples drops only the marker lines.
    /// </summary>
    [Fact]
    public async Task Should_Keep_The_Content_And_Drop_The_Markers_When_Examples_Are_Kept()
    {
        var path = await WriteAsync("nav-items.ts", NavItems);

        await CreateProjectGenerator.StripExampleRegionsAsync(_webPath, keepExamples: true);

        Assert.Equal("""
            import { Home } from 'lucide-react'
            import { Palette } from 'lucide-react'

            export const navItems = [
              { title: 'navigation.dashboard', icon: Home },
              { title: 'navigation.uiCards', icon: Palette },
            ]

            """, await File.ReadAllTextAsync(path));
    }

    /// <summary>
    /// Tests that a CSS region, marked with block comments, is removed, and CRLF line endings survive.
    /// </summary>
    [Fact]
    public async Task Should_Remove_A_Css_Region_And_Keep_Crlf_Line_Endings()
    {
        var path = await WriteAsync(Path.Combine("styles", "tailwind.css"),
            ".a {}\r\n  /* efn:examples:start */\r\n  .table-base {}\r\n  /* efn:examples:end */\r\n.b {}\r\n");

        await CreateProjectGenerator.StripExampleRegionsAsync(_webPath, keepExamples: false);

        Assert.Equal(".a {}\r\n.b {}\r\n", await File.ReadAllTextAsync(path));
    }

    /// <summary>
    /// Tests that files without a marker, and files under node_modules, are left untouched.
    /// </summary>
    [Fact]
    public async Task Should_Leave_Unmarked_And_Dependency_Files_Alone()
    {
        const string plain = "export const a = 1\n";
        var unmarked = await WriteAsync("plain.ts", plain);
        var dependency = await WriteAsync(Path.Combine("node_modules", "pkg", "index.ts"), NavItems);

        await CreateProjectGenerator.StripExampleRegionsAsync(_webPath, keepExamples: false);

        Assert.Equal(plain, await File.ReadAllTextAsync(unmarked));
        Assert.Equal(NavItems, await File.ReadAllTextAsync(dependency));
    }

    /// <summary>
    /// Tests that an unclosed, nested or stray marker fails generation instead of passing silently.
    /// </summary>
    [Theory]
    [InlineData("// efn:examples:start\nconst a = 1\n")]
    [InlineData("// efn:examples:start\n// efn:examples:start\n// efn:examples:end\n")]
    [InlineData("const a = 1\n// efn:examples:end\n")]
    public async Task Should_Refuse_An_Unbalanced_Marker(string content)
    {
        await WriteAsync("broken.ts", content);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProjectGenerator.StripExampleRegionsAsync(_webPath, keepExamples: false));
    }

    private async Task<string> WriteAsync(string relativePath, string content)
    {
        var path = Path.Combine(_webPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
