namespace Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// This route group that prefixes all tenant administration endpoints - tenant lifecycle, tenant
/// membership, self-service onboarding and tenant switching - with the <c>tenants</c> segment.
/// Endpoints in this area declare only the remainder of their route.
/// </summary>
sealed class TenantsGroup : Group
{
    public TenantsGroup()
    {
        Configure("tenants", ep => {});
    }
}