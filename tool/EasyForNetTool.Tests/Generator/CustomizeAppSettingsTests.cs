using System.Text.Json;
using EasyForNetTool.Generator;

namespace EasyForNetTool.Tests.Generator;

/// <summary>
/// Unit tests for <see cref="CreateProjectGenerator.CustomizeAppSettingsAsync"/>.
/// </summary>
public class CustomizeAppSettingsTests
{
    private const string Template = """
        {
          "ConnectionStrings": {
            "DefaultConnection": "Host=localhost;Database=Backend;Password={password}"
          },
          "Hangfire": { "Storage": { "ConnectionString": "Host=localhost;Database=Backend;Password={password}" } },
          "Auth": { "Jwt": { "Key": "placeholder" } },
          "Redis": { "InstanceName": "EasyForNet:" }
        }
        """;

    /// <summary>
    /// Tests that each generated project gets its own Redis key prefix, with a separate one for Testing.
    /// </summary>
    [Fact]
    public async Task Should_Set_Redis_InstanceName_Per_Environment()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), "efn-appsettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
                await File.WriteAllTextAsync(Path.Combine(dir, name), Template, TestContext.Current.CancellationToken);

            // Act
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "demo", DevPorts.Default);

            // Assert
            Assert.Equal("demo:", await ReadAsync(dir, "appsettings.json", "Redis", "InstanceName"));
            Assert.Equal("demo:", await ReadAsync(dir, "appsettings.Development.json", "Redis", "InstanceName"));
            Assert.Equal("demo_test:", await ReadAsync(dir, "appsettings.Testing.json", "Redis", "InstanceName"));
            Assert.Contains("Database=demo_test;", await ReadAsync(dir, "appsettings.Testing.json", "ConnectionStrings", "DefaultConnection"));
            Assert.Contains("Database=demo;", await ReadAsync(dir, "appsettings.json", "ConnectionStrings", "DefaultConnection"));
            Assert.NotEqual("placeholder", await ReadAsync(dir, "appsettings.Development.json", "Auth", "Jwt", "Key"));
            Assert.NotEqual("placeholder", await ReadAsync(dir, "appsettings.Testing.json", "Auth", "Jwt", "Key"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests the rewrite against the template's own appsettings.json, which the generator copies into all
    /// three files, so a renamed or removed Redis section fails here instead of being skipped silently.
    /// </summary>
    [Fact]
    public async Task Should_Set_Redis_InstanceName_In_Template_AppSettings()
    {
        // Arrange
        var templateSettings = FindTemplateAppSettings();
        var dir = Path.Combine(Path.GetTempPath(), "efn-appsettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
                File.Copy(templateSettings, Path.Combine(dir, name));

            // Act
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "demo", DevPorts.Default);

            // Assert
            Assert.Equal("demo:", await ReadAsync(dir, "appsettings.json", "Redis", "InstanceName"));
            Assert.Equal("demo:", await ReadAsync(dir, "appsettings.Development.json", "Redis", "InstanceName"));
            Assert.Equal("demo_test:", await ReadAsync(dir, "appsettings.Testing.json", "Redis", "InstanceName"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that every project seeds its administrators with the fixed local password the generated
    /// CLAUDE.md names - the same in all three files, so the tests sign in with what Development seeds -
    /// and that Development and Testing connect with the docker-compose.yml password while the tracked
    /// appsettings.json keeps its placeholder.
    /// </summary>
    [Fact]
    public async Task Should_Set_Local_Seed_Password_And_Development_Database_Password()
    {
        // Arrange
        var templateSettings = FindTemplateAppSettings();
        var dir = Path.Combine(Path.GetTempPath(), "efn-appsettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
                File.Copy(templateSettings, Path.Combine(dir, name));

            // Act
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "demo", DevPorts.Default);

            // Assert
            const string password = "Admin#123";
            Assert.Equal(password, CreateProjectGenerator.LocalAdminPassword);
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
            {
                Assert.Equal(password, await ReadAsync(dir, name, "Seed", "PlatformAdminPassword"));
                Assert.Equal(password, await ReadAsync(dir, name, "Seed", "TenantAdminPassword"));
            }
            Assert.Contains("Password={password}", await ReadAsync(dir, "appsettings.json", "ConnectionStrings", "DefaultConnection"));
            foreach (var name in new[] { "appsettings.Development.json", "appsettings.Testing.json" })
            {
                Assert.EndsWith($"Password={CreateProjectGenerator.DevelopmentDatabasePassword}", await ReadAsync(dir, name, "ConnectionStrings", "DefaultConnection"));
                Assert.EndsWith($"Password={CreateProjectGenerator.DevelopmentDatabasePassword}", await ReadAsync(dir, name, "Hangfire", "Storage", "ConnectionString"));
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that the ports picked for the development containers reach every connection string, so the
    /// API connects where the root .env publishes PostgreSQL and Redis.
    /// </summary>
    [Fact]
    public async Task Should_Write_Dev_Ports_Into_Every_Connection_String()
    {
        // Arrange
        var templateSettings = FindTemplateAppSettings();
        var dir = Path.Combine(Path.GetTempPath(), "efn-appsettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
                File.Copy(templateSettings, Path.Combine(dir, name));

            // Act
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "demo", new DevPorts(5433, 6380));

            // Assert
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
            {
                Assert.Contains("Port=5433;", await ReadAsync(dir, name, "ConnectionStrings", "DefaultConnection"));
                Assert.Contains("Port=5433;", await ReadAsync(dir, name, "Hangfire", "Storage", "ConnectionString"));
                Assert.Equal($"localhost:6380,password={CreateProjectGenerator.DevelopmentRedisPassword}", await ReadAsync(dir, name, "ConnectionStrings", "Redis"));
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that the Web origins of appsettings.json and appsettings.Development.json name the proxy port
    /// picked for the project, and that Testing keeps the template's.
    /// </summary>
    [Fact]
    public async Task Should_Write_The_Proxy_Port_Into_The_Web_Origins()
    {
        // Arrange
        var templateSettings = FindTemplateAppSettings();
        var dir = Path.Combine(Path.GetTempPath(), "efn-appsettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Testing.json" })
                File.Copy(templateSettings, Path.Combine(dir, name));

            // Act
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "demo", DevPorts.Default with { Proxy = 3005 });

            // Assert
            foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
            {
                Assert.Equal("http://app.localhost:3005", await ReadAsync(dir, name, "Web", "DefaultDomain"));
                Assert.Equal("http://{tenant}.localhost:3005", await ReadAsync(dir, name, "Web", "TenantOriginTemplate"));
                Assert.Equal("http://localhost:3005", await ReadAsync(dir, name, "Web", "Domains", "0"));
            }
            Assert.Equal("http://app.localhost:3000", await ReadAsync(dir, "appsettings.Testing.json", "Web", "DefaultDomain"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that only localhost origins take the port, whatever port they named before.
    /// </summary>
    [Theory]
    [InlineData("http://localhost:3000", "http://localhost:4100")]
    [InlineData("http://localhost", "http://localhost:4100")]
    [InlineData("http://app.localhost:3000/", "http://app.localhost:4100/")]
    [InlineData("http://{tenant}.localhost:3000", "http://{tenant}.localhost:4100")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://localhost.example.com:3000", "http://localhost.example.com:3000")]
    public void Should_Set_The_Port_Of_Localhost_Origins_Only(string origin, string expected)
    {
        Assert.Equal(expected, CreateProjectGenerator.WithLocalOriginPort(origin, 4100));
    }

    /// <summary>
    /// Tests that the default ports in a document are replaced in one pass, so a proxy moved onto 3001
    /// does not then move again with next dev.
    /// </summary>
    [Fact]
    public void Should_Rewrite_Default_Dev_Ports_In_Text_Once()
    {
        // Arrange
        var text = "the API on :5000, next dev on :3001, open http://default.localhost:3000 (`:3000`); port 30001 and 5000ms stay";

        // Act
        var rewritten = CreateProjectGenerator.WithDevPorts(text, new DevPorts(5432, 6379, 3001, 3002, 5001));

        // Assert
        Assert.Equal("the API on :5001, next dev on :3002, open http://default.localhost:3001 (`:3001`); port 30001 and 5000ms stay", rewritten);
    }

    internal static string FindTemplateFile(params string[] relativePath)
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            var candidate = Path.Combine([current.FullName, .. relativePath]);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"The template's {string.Join('/', relativePath)} was not found above the test output directory.");
    }

    private static string FindTemplateAppSettings()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "src", "multi-tenant", "backend", "Source", "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("The template's src/multi-tenant/backend/Source/appsettings.json was not found above the test output directory.");
    }

    private static async Task<string> ReadAsync(string dir, string file, params string[] path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, file), TestContext.Current.CancellationToken));
        var element = doc.RootElement;
        foreach (var key in path)
            element = element.ValueKind == JsonValueKind.Array ? element[int.Parse(key)] : element.GetProperty(key);
        return element.GetString()!;
    }
}
