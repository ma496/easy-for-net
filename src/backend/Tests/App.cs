namespace Backend.Tests;

using System.Reflection;
using Backend.Tests.Architect;
using Microsoft.AspNetCore.Hosting;

/// <summary>
/// Main application fixture for the backend test project.
/// Configures the test host and registers test-specific services.
/// Re-implements <see cref="IAsyncLifetime"/> so that source project startup exceptions
/// are caught silently, allowing per-test skip via <see cref="AppTestsBase.SetupAsync"/>.
/// </summary>
public class App : AppFixture<Program>, IAsyncLifetime
{
    private static readonly MethodInfo _baseInitializeAsync = ((Func<MethodInfo>)(() =>
    {
        var ifaceMap = typeof(AppFixture<Program>).GetInterfaceMap(typeof(IAsyncLifetime));
        return ifaceMap.TargetMethods[0];
    }))();

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        try
        {
            await (ValueTask)_baseInitializeAsync.Invoke(this, null)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            SharedContextFixture.InitializationError = $"App fixture initialization failed: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        }
        catch (Exception ex)
        {
            SharedContextFixture.InitializationError = $"App fixture initialization failed: {ex.GetType().Name}: {ex.Message}";
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
    }
}
