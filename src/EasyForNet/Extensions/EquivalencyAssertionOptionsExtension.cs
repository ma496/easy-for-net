using FluentAssertions;
using FluentAssertions.Equivalency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyForNet.Extensions
{
    public static class EquivalencyAssertionOptionsExtension
    {
        public static EquivalencyAssertionOptions<T> BeCloseToDateTime<T>(this EquivalencyAssertionOptions<T> options, TimeSpan timeSpan)
            where T : class
        {
            return options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, timeSpan)).WhenTypeIs<DateTime>();
        }
    }
}
