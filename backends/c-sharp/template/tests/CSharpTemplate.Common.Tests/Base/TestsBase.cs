using EasyForNet.Tests.Share.Common;
using Xunit.Abstractions;

namespace CSharpTemplate.Common.Tests.Base;

[Collection(nameof(CSharpTemplateCommonTestsCollectionFixture))]
public abstract class TestsBase : TestsCommon
{
    protected TestsBase(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }
}