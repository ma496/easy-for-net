namespace EasyForNetTool.Tests.Generator;

using EasyForNetTool.Generator;

/// <summary>
/// Unit tests for <see cref="CreateProjectGenerator.WriteDockerEnvAsync"/>, run against the template's own
/// <c>.env.docker.example</c> so a renamed or added key fails here instead of shipping empty.
/// </summary>
public class WriteDockerEnvTests
{
    /// <summary>
    /// Tests that every value docker-compose.prod.yml requires is filled in for the project, and that the
    /// example's comments are kept.
    /// </summary>
    [Fact]
    public async Task Should_Fill_Every_Required_Value()
    {
        // Arrange
        var example = CustomizeAppSettingsTests.FindTemplateFile(".env.docker.example");
        var envPath = Path.Combine(Path.GetTempPath(), "efn-env-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Act
            await CreateProjectGenerator.WriteDockerEnvAsync(example, envPath, "demo-shop", "DemoShop");

            // Assert
            var lines = await File.ReadAllLinesAsync(envPath);
            var values = Parse(lines);
            Assert.Equal("demo-shop", values["PROD_PROJECT_NAME"]);
            Assert.Equal("localhost", values["DOMAIN"]);
            Assert.Equal("https://localhost", values["PUBLIC_URL"]);
            Assert.Equal("demo_shop", values["POSTGRES_DB"]);
            Assert.Equal("demo_shop", values["POSTGRES_USER"]);
            Assert.Equal("DemoShop:", values["REDIS_INSTANCE_NAME"]);
            Assert.Equal(32, values["POSTGRES_PASSWORD"].Length);
            Assert.Equal(32, values["REDIS_PASSWORD"].Length);
            Assert.True(values["JWT_KEY"].Length >= 32);
            Assert.InRange(values["SEED_ADMIN_PASSWORD"].Length, 8, 50);
            Assert.Equal(Parse(await File.ReadAllLinesAsync(example)).Keys.Order(), values.Keys.Order());
            Assert.Equal(File.ReadAllLines(example).Count(line => line.StartsWith('#')), lines.Count(line => line.StartsWith('#')));
        }
        finally
        {
            File.Delete(envPath);
        }
    }

    /// <summary>
    /// Tests that two projects never share a secret.
    /// </summary>
    [Fact]
    public async Task Should_Generate_Different_Secrets_Each_Time()
    {
        // Arrange
        var example = CustomizeAppSettingsTests.FindTemplateFile(".env.docker.example");
        var first = Path.Combine(Path.GetTempPath(), "efn-env-" + Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), "efn-env-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Act
            await CreateProjectGenerator.WriteDockerEnvAsync(example, first, "demo", "Demo");
            await CreateProjectGenerator.WriteDockerEnvAsync(example, second, "demo", "Demo");

            // Assert
            var a = Parse(await File.ReadAllLinesAsync(first));
            var b = Parse(await File.ReadAllLinesAsync(second));
            foreach (var key in new[] { "POSTGRES_PASSWORD", "REDIS_PASSWORD", "JWT_KEY", "SEED_ADMIN_PASSWORD" })
                Assert.NotEqual(a[key], b[key]);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    private static Dictionary<string, string> Parse(IEnumerable<string> lines)
        => lines
            .Where(line => line.Length > 0 && !line.StartsWith('#') && line.Contains('='))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);
}
