using Autofac;
using AutoMapper;
using AutoMapper.Configuration.Annotations;
using EasyForNet.Tests.Base;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Application.Mapping
{
    public class AutoMapToTests : TestsBase
    {
        public AutoMapToTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestOne()
        {
            var mapper = Scope.Resolve<IMapper>();

            var classOne = new ClassOne
            {
                PropOne = "PropOne",
                PropTwo = "PropTwo",
                PropThree = "PropThree"
            };
            var classTwo = mapper.Map<ClassTwo>(classOne);

            classOne.Should().BeEquivalentTo(classTwo, opt => opt.Excluding(x => x.PropFour));
        }

        public class ClassOne
        {
            public string PropOne { get; set; }
            public string PropTwo { get; set; }
            public string PropThree { get; set; }
        }

        [AutoMap(typeof(ClassOne))]
        public class ClassTwo
        {
            public string PropOne { get; set; }
            public string PropTwo { get; set; }
            public string PropThree { get; set; }
            [Ignore] public string PropFour { get; set; }
        }
    }
}