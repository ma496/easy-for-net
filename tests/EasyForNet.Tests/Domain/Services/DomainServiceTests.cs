using System;
using EasyForNet.Domain.Services;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Domain.Services
{
    public class DomainServiceTests : TestsBase
    {
        public DomainServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestOne()
        {
            var domainService = Services.GetRequiredService<DomainServiceDemo>();

            Assert.NotNull(domainService);
        }
    }

    public class DomainServiceDemo : DomainService
    {
        public DomainServiceDemo(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
    }
}