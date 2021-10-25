using EasyForNet.Tests.Share.Common;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Base
{
    [Collection(nameof(EasyForNetTestsCollectionFixture))]
    public abstract class TestsBase : TestsCommon
    {
        protected TestsBase(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}