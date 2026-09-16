namespace Backend.Features.Tenancy;

using Backend.Features.Tenancy.Core;

/// <summary>
/// Feature module that registers the tenancy services with the DI container.
/// </summary>
/// <remarks>
/// Both services are registered scoped, matching the scoped <c>AppDbContext</c> and
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
    }
}