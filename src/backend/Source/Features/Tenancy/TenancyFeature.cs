namespace Backend.Features.Tenancy;

using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Core.FeatureManagement;
using Backend.Features.Tenancy.Core.FeatureManagement.Providers;

/// <summary>
/// Feature module that registers the tenancy services with the DI container.
/// </summary>
/// <remarks>
/// Every service here is registered scoped, matching the scoped <c>AppDbContext</c> and
/// <c>ITenantContext</c> they depend on: an HTTP request and a background job each resolve their own
/// instance, so neither can carry the other's tenant scope into its work.
/// </remarks>
[BypassNoDirectUse]
public class TenancyFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        // configure services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();
        services.AddScoped<ITenantMembershipQuery, TenantMembershipQuery>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IEditionService, EditionService>();

        AddFeatureManagement(services);
    }

    /// <summary>
    /// Registers the entitlement system: the catalogue of features the slices declare, and the chain
    /// that resolves what one tenant's plan is worth.
    /// </summary>
    /// <remarks>
    /// The definition providers are discovered by reflection across the whole assembly, because every
    /// slice declares the features it owns - the same shape the permission providers are registered
    /// in. Only the declarations come from outside; everything that resolves or stores a value belongs
    /// to this slice, since a feature value is a fact about a tenant or the plan it is on.
    /// </remarks>
    /// <param name="services">The container being populated.</param>
    private static void AddFeatureManagement(IServiceCollection services)
    {
        var featureProviders = typeof(TenancyFeature).Assembly.GetTypes()
            .Where(t => typeof(IFeatureDefinitionProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        foreach (var provider in featureProviders)
        {
            services.AddSingleton(typeof(IFeatureDefinitionProvider), provider);
        }

        // Singleton: the catalogue is code, so it cannot change while the process runs, and it is
        // composed once rather than on every session mint.
        services.AddSingleton<IFeatureDefinitionService, FeatureDefinitionService>();

        // Registration order is the fallback order: a tenant's own override beats what its edition
        // grants, which beats what the deployment configured, which beats what the definition declares.
        services.AddScoped<IFeatureValueProvider, TenantFeatureValueProvider>();
        services.AddScoped<IFeatureValueProvider, EditionFeatureValueProvider>();
        services.AddScoped<IFeatureValueProvider, ConfigurationFeatureValueProvider>();
        services.AddScoped<IFeatureValueProvider, DefaultValueFeatureValueProvider>();

        services.AddScoped<IFeatureValueStore, FeatureValueStore>();
        services.AddScoped<IFeatureValueResolver, FeatureValueResolver>();
        services.AddScoped<IFeatureChecker, FeatureChecker>();
        services.AddScoped<IPermissionFeatureFilter, PermissionFeatureFilter>();
    }
}