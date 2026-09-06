namespace Backend.Tests;

using Microsoft.AspNetCore.Hosting;

/// <summary>
/// Shared fixture that seeds test data and authenticates once for the entire test collection.
/// The database is deleted during teardown.
/// </summary>
public class SharedContextFixture : AppFixture<Program>
{
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

    protected override void ConfigureApp(IWebHostBuilder app)
    {
        app.UseEnvironment("Testing");
    }

    private async Task DeleteDatabaseAsync()
    {
        var dbContext = Services.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
    }
}
