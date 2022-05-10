using System;
using AutoMapper;
using EasyForNet.Modules;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Modules
{
    // ReSharper disable once InconsistentNaming
    public class ModuleInitializer_InitMappingsTests : TestsBase
    {
        public ModuleInitializer_InitMappingsTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void InitMappings_AgainstMoreThenOne()
        {
            Assert.Throws<Exception>(() =>
            {
                ModuleInitializer.InitMappings<TempModule>(new MapperConfigurationExpression(), null);
                ModuleInitializer.InitMappings<TempModule>(new MapperConfigurationExpression(), null);
            });
        }

        private class TempModule : ModuleBase
        {
        }
    }
}