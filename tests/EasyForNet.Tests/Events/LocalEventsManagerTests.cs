using System;
using System.Threading.Tasks;
using EasyForNet.Events.Local;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Events
{
    public class LocalEventsManagerTests : TestsBase
    {
        public LocalEventsManagerTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
        
        [Fact]
        public async Task LocalEventsTest()
        {
            var eventsManager = Services.GetRequiredService<ILocalEventsManager>();
            
            await eventsManager.RaiseAsync(new ProductEvent());
            
            Assert.True(ProductLocalEventHandler.IsOccur);
            Assert.True(ProductOneLocalEventHandler.IsOccur);
        }
        
        public class ProductLocalEventHandler : LocalEventHandler<ProductEvent>
        {
            public static bool IsOccur;
            
            public ProductLocalEventHandler(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }

            public override async Task HandleAsync(ProductEvent @event)
            {
                IsOccur = true;
                await Task.CompletedTask;
            }
        }
        
        public class ProductOneLocalEventHandler : LocalEventHandler<ProductEvent>
        {
            public static bool IsOccur;
            
            public ProductOneLocalEventHandler(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }
            
            public override async Task HandleAsync(ProductEvent @event)
            {
                IsOccur = true;
                await Task.CompletedTask;
            }
        }
        
        public class ProductEvent
        {
        }
    }
}