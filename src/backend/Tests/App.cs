namespace Backend.Tests;

using Backend.Tests.Architect;
using Microsoft.AspNetCore.Hosting;

/// <summary>
/// Main application fixture for the backend test project.
/// Configures the test host and registers test-specific services.
/// </summary>
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
    }
}
