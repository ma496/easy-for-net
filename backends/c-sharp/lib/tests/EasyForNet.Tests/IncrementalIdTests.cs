using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests;

public class IncrementalIdTests : TestsBase
{
    public IncrementalIdTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void IdTest()
    {
        var id = IncrementalId.Id;
        var idOne = IncrementalId.Id;

        Assert.Equal(id + 1, idOne);
    }
}