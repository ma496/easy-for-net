using Autofac;
using Autofac.Extensions.DependencyInjection;
using CSharpTemplate.App.Data;
using CSharpTemplate.DbMigrator;
using EasyForNet.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

await Start();

async Task Start()
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/dbMigrator.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();
    
    var services = new ServiceCollection()
        .AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddSerilog();
        });

    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", true, true);

    var configuration = builder.Build();
    
    var cb = new ContainerBuilder();
    AppInitializer.Init<CSharpTemplateDbMigratorModule>(cb, configuration);
    cb.Populate(services);
    
    var container = cb.Build();

    await using var scope = container.BeginLifetimeScope();
    
    var dbManager = scope.Resolve<CSharpTemplateDbManager>();
    await dbManager.MigrateAsync();

    await Log.CloseAndFlushAsync();
}