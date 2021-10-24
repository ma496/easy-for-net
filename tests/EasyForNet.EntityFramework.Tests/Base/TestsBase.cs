using EasyForNet.Tests.Share.Common;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Base
{
    [Collection(nameof(EasyForNetEntityFrameworkTestsCollectionFixture))]
    public abstract class TestsBase : TestsCommon
    {
        protected TestsBase(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }
    }
}