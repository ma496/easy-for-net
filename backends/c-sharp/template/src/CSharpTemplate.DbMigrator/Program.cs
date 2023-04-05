using Autofac;
using Autofac.Extensions.DependencyInjection;
using CSharpTemplate.Common.Identity.Permissions.Provider;
using CSharpTemplate.Data.Context;
using CSharpTemplate.DbMigrator;
using EasyForNet.Data;
using EasyForNet.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

await Start();

async Task Start()
{
    var services = new ServiceCollection();

    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", true, true);

    var configuration = builder.Build();
    
    var cb = new ContainerBuilder();
    AppInitializer.Init<CSharpTemplateDbMigratorModule>(cb, configuration);
    cb.Populate(services);
    
    var container = cb.Build();

    await using var scope = container.BeginLifetimeScope();
    
    var dbContext = scope.Resolve<CSharpTemplateDbContext>();
    await dbContext.Database.MigrateAsync();

    var permissionsContext = scope.Resolve<IPermissionsContext>();
    var permissionsProvider = scope.Resolve<IPermissionsProvider>();
    permissionsProvider.Permissions(permissionsContext);
    
    var dataSeeders = scope.Resolve<IEnumerable<IDataSeeder>>();
    Console.WriteLine("Seed the data");
    foreach (var dataSeeder in dataSeeders)
    {
        await dataSeeder.SeedAsync();
    }
}