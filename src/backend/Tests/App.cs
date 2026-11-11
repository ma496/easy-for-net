using Backend.Features.FileManagement.Core;
using Backend.Tests.Architect;
using Backend.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;

namespace Backend.Tests;

/// <summary>
/// The application under test: one host, booted once for the whole assembly, migrated and seeded
/// before the first test and dropped after the last.
/// </summary>
/// <remarks>
/// <para>
/// It is registered as an assembly fixture in <c>Meta.cs</c> rather than as a class or collection
/// fixture, and that is what lets the suite run classes concurrently. A collection fixture is built
/// once per collection, so several collections sharing one would each seed the database again and -
/// worse - each drop it while the others were still running. An assembly fixture is built once, no
/// matter how the classes are grouped.
/// </para>
/// <para>
/// Everything a test needs from the host is reached through <see cref="AppTestsBase"/>, which gives
/// each test its own client and its own service scope. Nothing here is per-test state.
/// </para>
/// </remarks>
public class App : AppFixture<Program>
{
    private TestDatabaseTemplate? _template;

    /// <summary>
    /// Clones the test database from its template, when a current one exists, before the host starts
    /// and migrates it - see <see cref="TestDatabaseTemplate"/>.
    /// </summary>
    protected override async ValueTask PreSetupAsync()
    {
        _template = TestDatabaseTemplate.Create();
        if (_template is not null)
        {
            await _template.RestoreAsync();
        }
    }

    /// <summary>
    /// Configures the web host builder for the test application.
    /// </summary>
    protected override void ConfigureApp(IWebHostBuilder a)
    {
        a.UseEnvironment("Testing");
    }

    /// <summary>
    /// Registers test-specific services into the dependency injection container.
    /// </summary>
    protected override void ConfigureServices(IServiceCollection s)
    {
        s.AddScoped<IFeatureDependencyTester, FeatureDependencyTester>();
        s.AddScoped<TestsDataSeeder>();
        s.RegisterTestDoubles();
    }

    /// <summary>
    /// Seeds the data every test reads. Migrations are not applied here: the host applies them on
    /// startup under the Testing environment, which has already happened by the time this runs.
    /// </summary>
    protected override async ValueTask SetupAsync()
    {
        // Saved before the test seed, so the template holds only what the host itself makes.
        if (_template is not null)
        {
            await _template.CaptureAsync();
        }

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TestsDataSeeder>().SeedAsync();
    }

    /// <summary>
    /// Drops the database so the next run starts from migrations rather than from what this one left.
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
        await DeleteLeftoverUploadsAsync();

        try
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
        catch (Exception exception)
        {
            // Reported rather than swallowed: a drop that keeps failing leaves the next run to
            // inherit everything this one wrote, and the symptom of that is a suite that grows
            // slower and starts failing on rows it did not create.
            Console.WriteLine($"The test database could not be dropped: {exception.Message}");
        }
    }

    /// <summary>
    /// Removes every file the run stored that is still in storage. Each test deletes its own uploads
    /// (<see cref="AppTestsBase.DeleteAfterTest"/>), so anything found here was missed by its test, and
    /// the count is reported so the missing call can be added.
    /// </summary>
    private async Task DeleteLeftoverUploadsAsync()
    {
        try
        {
            await using var scope = Services.CreateAsyncScope();
            var storageProvider = scope.ServiceProvider.GetRequiredService<IStorageProvider>();
            var leftovers = Services.GetRequiredService<UploadedFilesRecorder>().FileNames
                .Where(storageProvider.Exists)
                .ToList();

            foreach (var fileName in leftovers)
            {
                await storageProvider.DeleteAsync(fileName);
            }

            if (leftovers.Count > 0)
            {
                Console.WriteLine($"{leftovers.Count} uploaded files were left by their test and removed at teardown.");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"The uploaded files could not be removed: {exception.Message}");
        }
    }

    /// <summary>
    /// Hidden on purpose. One client shared by 377 tests is one bearer token shared by 377 tests, and
    /// the moment two of them run at once they authenticate as each other. Use
    /// <see cref="AppTestsBase.Client"/>, which belongs to a single test, or
    /// <see cref="AppFixture{TProgram}.CreateClient(FastEndpoints.Testing.ClientOptions)"/> for a
    /// second identity within one test.
    /// </summary>
    [Obsolete("Use the per-test AppTestsBase.Client instead of the fixture's shared client.", error: true)]
    public new HttpClient Client => base.Client;
}
