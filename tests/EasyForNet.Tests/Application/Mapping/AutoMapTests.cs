using AutoMapper;
using EasyForNet.Tests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Application.Mapping
{
    public class AutoMapTests : TestsBase
    {
        public AutoMapTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestOne()
        {
            var mapper = Services.GetRequiredService<IMapper>();

            var classOne = new ClassOne
            {
                PropOne = "PropOne",
                PropTwo = "PropTwo",
                PropThree = "PropThree"
            };
            var classTwo = mapper.Map<ClassTwo>(classOne);
            classOne = mapper.Map<ClassOne>(classTwo);

            CompareAssert(classOne, classTwo);
        }

        [AutoMap(typeof(ClassTwo), ReverseMap = true)]
        public class ClassOne
        {
            public string PropOne { get; set; }
            public string PropTwo { get; set; }
            public string PropThree { get; set; }
        }

        public class ClassTwo
        {
            public string PropOne { get; set; }
            public string PropTwo { get; set; }
            public string PropThree { get; set; }
        }
    }
}