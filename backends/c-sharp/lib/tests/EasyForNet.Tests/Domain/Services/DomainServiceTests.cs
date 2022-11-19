using System;
using Autofac;
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
            var domainService = Scope.Resolve<DomainServiceDemo>();

            Assert.NotNull(domainService);
        }
    }

    public class DomainServiceDemo : DomainService
    {
    }
}