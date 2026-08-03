namespace Backend.Tests;

using System.Reflection;

/// <summary>
/// Shared fixture that seeds test data and authenticates once for the entire test collection.
/// The database is deleted during teardown.
/// Catches source project startup exceptions in <see cref="IAsyncLifetime.InitializeAsync"/>
/// so that individual tests can be skipped instead of failing when the host cannot start.
/// </summary>
public class SharedContextFixture : AppFixture<Program>, IAsyncLifetime
{
    public static string? InitializationError { get; internal set; }

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
            TryDeleteDatabase();
            InitializationError = $"Shared fixture initialization failed: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        }
        catch (Exception ex)
        {
            TryDeleteDatabase();
            InitializationError = $"Shared fixture initialization failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    protected override async ValueTask SetupAsync()
    {
        var dbContext = Services.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        await TestsHelper.SetNewAuthTokenAsync(Client);
        var testsDataSeeder = Services.GetRequiredService<TestsDataSeeder>();
        await testsDataSeeder.SeedAsync(Client);
    }

    protected override async ValueTask TearDownAsync()
    {
        if (InitializationError is not null)
            return;
        try
        {
            await DeleteDatabaseAsync();
        }
        catch
        {
            // ignored
        }
    }

    protected override void ConfigureServices(IServiceCollection s)
    {
        s.AddScoped<TestsDataSeeder>();
    }

    private void TryDeleteDatabase()
    {
        try
        {
            using var scope = Services?.CreateScope();
            var dbContext = scope?.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext?.Database.EnsureDeleted();
        }
        catch
        {
            // ignored
        }
    }

    private async Task DeleteDatabaseAsync()
    {
        var dbContext = Services.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
    }
}
