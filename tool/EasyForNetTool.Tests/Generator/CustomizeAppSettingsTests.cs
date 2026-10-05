namespace EasyForNetTool.Tests.Generator;

using System.Text.Json;
using EasyForNetTool.Generator;

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
                await File.WriteAllTextAsync(Path.Combine(dir, name), Template);

            // Act
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "Demo", DevPorts.Default);

            // Assert
            Assert.Equal("Demo:", await ReadAsync(dir, "appsettings.json", "Redis", "InstanceName"));
            Assert.Equal("Demo:", await ReadAsync(dir, "appsettings.Development.json", "Redis", "InstanceName"));
            Assert.Equal("DemoTest:", await ReadAsync(dir, "appsettings.Testing.json", "Redis", "InstanceName"));
            Assert.Contains("Database=DemoTest;", await ReadAsync(dir, "appsettings.Testing.json", "ConnectionStrings", "DefaultConnection"));
            Assert.Contains("Database=Demo;", await ReadAsync(dir, "appsettings.json", "ConnectionStrings", "DefaultConnection"));
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
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "Demo", DevPorts.Default);

            // Assert
            Assert.Equal("Demo:", await ReadAsync(dir, "appsettings.json", "Redis", "InstanceName"));
            Assert.Equal("Demo:", await ReadAsync(dir, "appsettings.Development.json", "Redis", "InstanceName"));
            Assert.Equal("DemoTest:", await ReadAsync(dir, "appsettings.Testing.json", "Redis", "InstanceName"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Tests that a project gets its own random administrator password - the same in all three files, so
    /// the tests sign in with what Development seeds - and that Development and Testing connect with the
    /// docker-compose.yml password while the tracked appsettings.json keeps its placeholder.
    /// </summary>
    [Fact]
    public async Task Should_Set_Seed_Password_And_Development_Database_Password()
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
            var password = await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "Demo", DevPorts.Default);

            // Assert
            Assert.NotEqual("Admin#123", password);
            Assert.InRange(password.Length, 8, 50);
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
            Assert.NotEqual(password, await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "Demo", DevPorts.Default));
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
            await CreateProjectGenerator.CustomizeAppSettingsAsync(dir, "Demo", new DevPorts(5433, 6380));

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
            var candidate = Path.Combine(current.FullName, "src", "backend", "Source", "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("The template's src/backend/Source/appsettings.json was not found above the test output directory.");
    }

    private static async Task<string> ReadAsync(string dir, string file, params string[] path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, file)));
        var element = doc.RootElement;
        foreach (var key in path)
            element = element.GetProperty(key);
        return element.GetString()!;
    }
}
