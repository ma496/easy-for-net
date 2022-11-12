using System.IO;
using EasyForNet.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Tests.Share.Common
{
    public abstract class TestsFixtureCommon<TModule>
        where TModule : ModuleBase
    {
        protected TestsFixtureCommon()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true);

            var configuration = builder.Build();

            var services = new ServiceCollection();

            GlobalObjects.ServiceProvider = AppInitializer.Init<TModule>(configuration);
        }
    }
}