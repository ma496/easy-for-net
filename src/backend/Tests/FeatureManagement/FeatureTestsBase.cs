namespace Backend.Tests.FeatureManagement;

using Backend.Features.Tenancy.Core.Entities;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Base class for the feature-management suite.
/// </summary>
/// <remarks>
/// <para>
/// It builds on <see cref="TenancyTestsBase"/> rather than repeating its fixtures: almost every claim
/// about entitlements is a claim about what a caller in some tenant may do, so the tenants, roles,
/// accounts and sign-in helpers are exactly the ones needed here, and a second copy of them would be
/// a second set of rules about parallel safety to keep in step.
/// </para>
/// <para>
/// Every tenant, edition and stored value a test asserts on is made by that test. The suite runs in
/// parallel against one shared database, and a feature value is exactly the kind of row that changes
/// what another test's session is minted with - so a test that wrote one for a seeded tenant, or read
/// one it did not write, would be racing the rest of the run.
/// </para>
/// </remarks>
public abstract class FeatureTestsBase(App app) : TenancyTestsBase(app)
{
    protected IFeatureValueStore FeatureValueStore => Service<IFeatureValueStore>();

    protected IFeatureValueResolver FeatureValueResolver => Service<IFeatureValueResolver>();

    protected IFeatureDefinitionService FeatureDefinitions => Service<IFeatureDefinitionService>();

    protected IPermissionFeatureFilter PermissionFeatureFilter => Service<IPermissionFeatureFilter>();

    /// <summary>
    /// Creates an edition with a name no other test will take.
    /// </summary>
    /// <param name="name">A name to use, or <see langword="null"/> to generate one.</param>
    /// <returns>The created edition.</returns>
    protected async Task<Edition> CreateEditionAsync(string? name = null)
    {
        var edition = new Edition
        {
            Name = name ?? $"Edition {Guid.NewGuid():N}",
            Description = "Edition made by a feature-management test"
        };
        DbContext.Editions.Add(edition);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return edition;
    }

    /// <summary>
    /// Puts a tenant on an edition, or on none.
    /// </summary>
    /// <param name="tenantId">The tenant to move.</param>
    /// <param name="editionId">The plan to put it on, or <see langword="null"/> to take it off one.</param>
    protected async Task PutOnEditionAsync(Guid tenantId, Guid? editionId)
    {
        using var platformScope = TenantContext.BeginPlatformScope();

        var tenant = await DbContext.Tenants.SingleAsync(row => row.Id == tenantId,
                                                         TestContext.Current.CancellationToken);
        tenant.EditionId = editionId;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Creates a tenant already on an edition.
    /// </summary>
    /// <param name="editionId">The plan to put it on.</param>
    /// <returns>The created tenant.</returns>
    protected async Task<Tenant> CreateTenantOnEditionAsync(Guid editionId)
    {
        var tenant = await CreateTenantAsync();
        await PutOnEditionAsync(tenant.Id, editionId);
        return tenant;
    }

    /// <summary>
    /// Stores a feature value for a tenant.
    /// </summary>
    protected Task SetForTenantAsync(Guid tenantId, string feature, string? value)
        => FeatureValueStore.SetAsync(feature, value, FeatureValueProviderNames.Tenant, tenantId.ToString(),
                                      TestContext.Current.CancellationToken);

    /// <summary>
    /// Stores a feature value for an edition.
    /// </summary>
    protected Task SetForEditionAsync(Guid editionId, string feature, string? value)
        => FeatureValueStore.SetAsync(feature, value, FeatureValueProviderNames.Edition, editionId.ToString(),
                                      TestContext.Current.CancellationToken);

    /// <summary>
    /// Resolves every feature value for a tenant.
    /// </summary>
    protected Task<FeatureValueSet> ResolveForTenantAsync(Guid tenantId)
        => FeatureValueResolver.ResolveAsync(FeatureTarget.ForTenant(tenantId), TestContext.Current.CancellationToken);
}
