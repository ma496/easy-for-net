using Efn.Infrastructure.EfPersistence;
using Efn.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Efn.Tests.Setup;

public class EndpointsFixture(IMessageSink s) : TestFixture<Program>(s)
{
    protected override void ConfigureApp(IWebHostBuilder a)
    {
        base.ConfigureApp(a);
    }

    protected override async Task SetupAsync()
    {
        var dbContext = Services.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        var dataSeedManager = Services.GetRequiredService<IDataSeedManager>();
        await dataSeedManager.Seed();
    }

    protected override void ConfigureServices(IServiceCollection s)
    {
        // do test service registration here
    }

    protected override Task TearDownAsync()
    {
        // do cleanups here
        return Task.CompletedTask;
    }
}