namespace Backend.Tests.Features.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// Tests for <see cref="LocalizationService"/>'s own resolution cache: it has to key a resolved
/// culture's resources by the tenant they were resolved for as well as the culture, because
/// <see cref="ITenantContext.BeginTenant"/> and <see cref="ITenantContext.BeginPlatformScope"/> can name
/// a different scope more than once inside the lifetime of one scoped instance.
/// </summary>
public class LocalizationServiceTests(App app) : LocalizationTestsBase(app)
{
    /// <summary>
    /// Resolves the same culture for two different tenants, one after another, through the very same
    /// <see cref="ILocalizationService"/> instance - <c>Service&lt;T&gt;()</c> resolves scoped services
    /// from this test's own DI scope, so both calls share one cache. A cache keyed on culture alone
    /// would answer the second call with the first tenant's cached resolution instead of re-reading it.
    /// </summary>
    [Fact]
    public async Task Resolution_Cache_Does_Not_Leak_Across_A_Tenant_Switch_Within_One_Scope()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var key = AnyShippedKey();
        var cancellationToken = TestContext.Current.CancellationToken;

        await SetTenantTextAsync(tenantA.Id, Culture, key, "Tenant A's own text");
        await SetTenantTextAsync(tenantB.Id, Culture, key, "Tenant B's own text");

        var localizationService = Service<ILocalizationService>();

        ResolvedResources resolvedA;
        using (TenantContext.BeginTenant(tenantA.Id))
        {
            resolvedA = await localizationService.ResolveResourcesAsync(Culture, cancellationToken);
        }

        ResolvedResources resolvedB;
        using (TenantContext.BeginTenant(tenantB.Id))
        {
            resolvedB = await localizationService.ResolveResourcesAsync(Culture, cancellationToken);
        }

        resolvedA.Resources[key].Should().Be("Tenant A's own text");
        resolvedB.Resources[key].Should().Be("Tenant B's own text",
            "a cache keyed on culture alone would have answered from tenant A's cached resolution here");
    }
}
