using System.IO;
using EasyForNet.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Tests.Share.Common;

public abstract class TestsFixtureCommon<TModule>
    where TModule : ModuleBase
{
    protected TestsFixtureCommon()
    {
        var services = new ServiceCollection();

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);

        var configuration = builder.Build();

        AddServices(services);
        GlobalObjects.Container = AppInitializer.Init<TModule>(services, configuration);
    }

    protected virtual void AddServices(IServiceCollection services) { }
}