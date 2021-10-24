using System.IO;
using AutoMapper;
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

            ModuleInitializer.InitServices<TModule>(services, configuration);

            var mapperConfiguration =
                new MapperConfiguration(mc => ModuleInitializer.InitMappings<TModule>(mc, configuration));
            mapperConfiguration.AssertConfigurationIsValid();
            var mapper = mapperConfiguration.CreateMapper();
            services.AddSingleton(mapper);

            var serviceProvider = services.BuildServiceProvider();
            GlobalObjects.ServiceProvider = serviceProvider;
        }
    }
}