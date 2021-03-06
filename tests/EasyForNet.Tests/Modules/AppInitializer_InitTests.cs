using System;
using EasyForNet.Modules;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Modules
{
    // ReSharper disable once InconsistentNaming
    public class AppInitializer_InitTests : TestsBase
    {
        public AppInitializer_InitTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void Init_AgainstMoreThenOne()
        {
            var exception = Assert.Throws<Exception>(() =>
            {
                AppInitializer.Init<TempModule>();
                AppInitializer.Init<TempModule>();
            });

            Assert.Equal($"{typeof(TempModule).FullName} module already initialized",
                exception.Message);
        }

        // ReSharper disable once InconsistentNaming
        private class TempModule : ModuleBase
        {
        }
    }
}