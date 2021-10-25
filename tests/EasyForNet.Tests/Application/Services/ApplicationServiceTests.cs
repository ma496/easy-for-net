using System;
using EasyForNet.Application.Services;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Application.Services
{
    public class ApplicationServiceTests : TestsBase
    {
        public ApplicationServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
        
        [Fact]
        public void TestOne()
        {
            var service = Services.GetRequiredService<ApplicationServiceDemo>();
            
            Assert.NotNull(service);
        }
    }
    
    public class ApplicationServiceDemo : ApplicationService
    {
        public ApplicationServiceDemo(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
    }
}