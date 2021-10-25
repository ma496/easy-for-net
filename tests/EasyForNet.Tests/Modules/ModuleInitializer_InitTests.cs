using System;
using AutoMapper.Configuration;
using EasyForNet.Modules;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Modules
{
    // ReSharper disable once InconsistentNaming
    public class ModuleInitializer_InitTests : TestsBase
    {
        public ModuleInitializer_InitTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void InitServices_AgainstMoreThenOne()
        {
            var exception = Assert.Throws<Exception>(() =>
            {
                ModuleInitializer.InitServices<TempModule>(new ServiceCollection(), null);
                ModuleInitializer.InitServices<TempModule>(new ServiceCollection(), null);
            });

            Assert.Equal($"Services has already initialized for {typeof(TempModule).FullName} module",
                exception.Message);
        }

        [Fact]
        public void InitMappings_AgainstMoreThenOne()
        {
            var exception = Assert.Throws<Exception>(() =>
            {
                ModuleInitializer.InitMappings<TempModule>(new MapperConfigurationExpression(), null);
                ModuleInitializer.InitMappings<TempModule>(new MapperConfigurationExpression(), null);
            });

            Assert.Equal($"Mapping has already initialized for {typeof(TempModule).FullName} module",
                exception.Message);
        }

        // ReSharper disable once InconsistentNaming
        private class TempModule : ModuleBase
        {
        }
    }
}