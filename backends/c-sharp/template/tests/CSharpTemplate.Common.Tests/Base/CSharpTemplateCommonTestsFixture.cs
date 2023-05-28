using EasyForNet.Tests.Share.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CSharpTemplate.Common.Tests.Base;

public class CSharpTemplateCommonTestsFixture : TestsFixtureCommon<CSharpTemplateCommonTestsModule>
{
    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);
        services.AddDistributedMemoryCache();
    }
}