namespace Backend.Tests;

using Backend.Tests.Architect;
using Backend.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;

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
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TestsDataSeeder>().SeedAsync();
    }

    /// <summary>
    /// Drops the database so the next run starts from migrations rather than from what this one left.
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
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
    /// Hidden on purpose. One client shared by 377 tests is one bearer token shared by 377 tests, and
    /// the moment two of them run at once they authenticate as each other. Use
    /// <see cref="AppTestsBase.Client"/>, which belongs to a single test, or
    /// <see cref="AppFixture{TProgram}.CreateClient(FastEndpoints.Testing.ClientOptions)"/> for a
    /// second identity within one test.
    /// </summary>
    [Obsolete("Use the per-test AppTestsBase.Client instead of the fixture's shared client.", error: true)]
    public new HttpClient Client => base.Client;
}
