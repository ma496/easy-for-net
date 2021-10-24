using System;
using EasyForNet.Modules;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Modules
{
    // ReSharper disable once InconsistentNaming
    public class ModuleInitializer_InitServicesTests : TestsBase
    {
        public ModuleInitializer_InitServicesTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void InitServices_MoreThenOne()
        {
            Assert.Throws<Exception>(() =>
            {
                ModuleInitializer.InitServices<TempModule>(new ServiceCollection(), null);
                ModuleInitializer.InitServices<TempModule>(new ServiceCollection(), null);
            });
        }

        private class TempModule : ModuleBase
        {
        }
    }
}