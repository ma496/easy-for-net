using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Backend.Tests;

/// <summary>
/// A PostgreSQL copy of the test database as the host leaves it - migrated and seeded by
/// <c>DataSeeder</c>, before <see cref="Seeder.TestsDataSeeder"/> has written anything - that the next
/// run clones instead of building the same database again.
/// </summary>
/// <remarks>
/// <para>
/// Every run drops its database on teardown, so every run started from nothing: all the migrations,
/// the Hangfire schema, the platform seed. That fixed cost was paid by each <c>dotnet test</c>
/// whatever its filter, and an unattended task runs several. Cloning a template is a file copy on
/// the server, and the run still starts from a database no test has touched.
/// </para>
/// <para>
/// The template is keyed by what decides its contents - the migrations, the platform seeder and the
/// <c>Seed</c> configuration - so changing any of them builds a new one, and it is rebuilt at least
/// daily so a seed stamped with the time it ran never grows stale. Anything that goes wrong here is
/// reported and the run falls back to building the database the long way; a template is a
/// shortcut, never a requirement.
/// </para>
/// </remarks>
internal sealed class TestDatabaseTemplate
{
    private static readonly TimeSpan _maxAge = TimeSpan.FromHours(24);

    private const int FormatVersion = 2;

    private readonly string _serverConnectionString;
    private readonly string _database;
    private readonly string _template;

    private TestDatabaseTemplate(string serverConnectionString, string database, string template)
    {
        _serverConnectionString = serverConnectionString;
        _database = database;
        _template = template;
    }

    /// <summary>True when this run's database was cloned from the template rather than built.</summary>
    public bool Restored { get; private set; }

    /// <summary>
    /// True once the test database has been dropped for the host to build from migrations - the one
    /// case a template may be captured from.
    /// </summary>
    private bool _built;

    /// <summary>
    /// The template for this checkout's test database, or null when there is nothing to key it on
    /// (no repository around the test binaries, no connection string) - the run then builds as before.
    /// </summary>
    public static TestDatabaseTemplate? Create()
    {
        try
        {
            return CreateOrThrow();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"No test database template this run, building the database instead: {exception.Message}");
            return null;
        }
    }

    private static TestDatabaseTemplate? CreateOrThrow()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            return null;
        }

        // The same files and variables the host reads under Testing; the test output carries copies.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Testing.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            return null;
        }

        var database = builder.Database;
        builder.Database = "postgres";
        builder.Pooling = false;
        return new TestDatabaseTemplate(builder.ConnectionString, database, $"{database}_tpl_{Key(repositoryRoot, configuration)}");
    }

    /// <summary>
    /// Replaces the test database with a clone of a current template, before the host starts. With no
    /// current template it does nothing, and the host migrates and seeds as it always has.
    /// </summary>
    public async ValueTask RestoreAsync()
    {
        try
        {
            await using var connection = await OpenAsync();
            var createdAt = await TemplateCreatedAtAsync(connection);
            if (createdAt is null || DateTimeOffset.UtcNow - createdAt > _maxAge)
            {
                // A run killed before its teardown leaves its database behind, and the host would
                // migrate that in place - capturing every row its tests wrote into the template, and
                // failing every later run on the test seed it already holds. Dropping it makes the
                // host build from nothing, which is the only database worth capturing.
                await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {Quote(_database)} WITH (FORCE)");
                _built = true;
                return;
            }

            await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {Quote(_database)} WITH (FORCE)");
            await ExecuteAsync(connection, $"CREATE DATABASE {Quote(_database)} TEMPLATE {Quote(_template)}");
            Restored = true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"The test database template could not be used, building the database instead: {exception.Message}");
        }
    }

    /// <summary>
    /// Copies the database the host has just migrated and seeded into a new template, once, when this
    /// run had none to clone. Runs before the test seeder, so the template holds only what the host
    /// itself makes and every run's test seed starts from the same place.
    /// </summary>
    public async ValueTask CaptureAsync()
    {
        if (!_built)
        {
            return;
        }

        try
        {
            // A database being copied must have no other session. The host's pooled connections are
            // idle at this point; clearing the pools closes them rather than leaving them to be
            // broken underneath the first test.
            NpgsqlConnection.ClearAllPools();
            await using var connection = await OpenAsync();

            foreach (var stale in await TemplatesOfThisDatabaseAsync(connection))
            {
                await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {Quote(stale)} WITH (FORCE)");
            }

            await using (var terminate = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid()",
                connection))
            {
                terminate.Parameters.AddWithValue("database", _database);
                await terminate.ExecuteNonQueryAsync();
            }

            await ExecuteAsync(connection, $"CREATE DATABASE {Quote(_template)} TEMPLATE {Quote(_database)}");
            await ExecuteAsync(connection, $"COMMENT ON DATABASE {Quote(_template)} IS '{DateTimeOffset.UtcNow:O}'");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"The test database template could not be saved; the next run builds the database again: {exception.Message}");
        }
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>When the template was saved, or null when there is none (or it carries no stamp).</summary>
    private async Task<DateTimeOffset?> TemplateCreatedAtAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = @template",
            connection);
        command.Parameters.AddWithValue("template", _template);
        var stamp = await command.ExecuteScalarAsync();
        return stamp is string text && DateTimeOffset.TryParse(text, out var createdAt) ? createdAt : null;
    }

    /// <summary>Every template ever saved for this database, the current key's included.</summary>
    private async Task<List<string>> TemplatesOfThisDatabaseAsync(NpgsqlConnection connection)
    {
        var prefix = $"{_database}_tpl_";
        var names = new List<string>();
        await using var command = new NpgsqlCommand("SELECT datname FROM pg_database", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// Twelve hex characters over everything the template's contents follow from: each migration and
    /// the platform seeder (path and bytes), and the <c>Seed</c> configuration the seeded accounts are
    /// created with.
    /// </summary>
    private static string Key(string repositoryRoot, IConfiguration configuration)
    {
        var source = Path.Combine(repositoryRoot, "src", "backend", "Source");
        var files = new[] { "Migrations", "ShareData" }
            .Select(directory => Path.Combine(source, directory))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // Bumped when a template may have been captured wrongly, so every existing one goes stale.
        hash.AppendData(Encoding.UTF8.GetBytes($"format={FormatVersion};"));
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file));
            hash.AppendData(File.ReadAllBytes(Path.Combine(repositoryRoot, file)));
        }

        foreach (var setting in configuration.GetSection("Seed").AsEnumerable().OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes($"{setting.Key}={setting.Value};"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset())[..12];
    }

    /// <summary>The directory holding the solution, walking up from the test binaries.</summary>
    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
