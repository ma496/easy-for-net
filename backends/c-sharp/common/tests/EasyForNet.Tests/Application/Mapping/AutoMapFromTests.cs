using AutoMapper;
using AutoMapper.Configuration.Annotations;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Autofac;

namespace EasyForNet.Tests.Application.Mapping
{
    public class AutoMapFromTests : TestsBase
    {
        public AutoMapFromTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestOne()
        {
            var mapper = Scope.Resolve<IMapper>();

            var classTwo = new ClassTwo
            {
                PropOne = "PropOne",
                PropTwo = "PropTwo",
                PropThree = "PropThree"
            };
            var classOne = mapper.Map<ClassOne>(classTwo);

            classTwo.Should().BeEquivalentTo(classOne, opt => opt.Excluding(x => x.PropFour));
        }

        [AutoMap(typeof(ClassTwo))]
        public class ClassOne
        {
            public string PropOne { get; set; }
            public string PropTwo { get; set; }
            public string PropThree { get; set; }
            [Ignore] public string PropFour { get; set; }
        }

        public class ClassTwo
        {
            public string PropOne { get; set; }
            public string PropTwo { get; set; }
            public string PropThree { get; set; }
        }
    }
}