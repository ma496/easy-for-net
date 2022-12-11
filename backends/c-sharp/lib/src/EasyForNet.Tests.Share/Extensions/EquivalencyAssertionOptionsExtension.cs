using FluentAssertions;
using FluentAssertions.Equivalency;
using System;

namespace EasyForNet.Tests.Share.Extensions;

public static class EquivalencyAssertionOptionsExtension
{
    public static EquivalencyAssertionOptions<T> BeCloseTo<T>(this EquivalencyAssertionOptions<T> options, TimeSpan timeSpan)
        where T : class
    {
        return options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, timeSpan)).WhenTypeIs<DateTime>();
    }
}