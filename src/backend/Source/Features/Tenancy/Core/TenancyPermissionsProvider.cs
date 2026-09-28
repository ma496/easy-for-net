namespace Backend.Features.Tenancy.Core;

/// <summary>
/// Declares the permission hierarchy for the Tenancy feature: the tenant lifecycle and tenant
/// membership.
/// </summary>
/// <remarks>
/// The catalogue is declared here in code, so it is global, identical for every tenant and closed to
/// extension at run time. The tenant lifecycle is platform-scoped: listing every tenant there is,
/// creating one, renaming it, suspending, reactivating and deleting it are operations about the
/// platform rather than about any one tenant, so they are exercisable only by a platform account
/// acting in no tenant and are never offered on a tenant role's permission surface. Reading one
/// tenant in detail and administering its membership are exercisable in both scopes: a platform
/// account reaches any tenant that way, and a tenant administrator reaches their own.
/// <para>
/// Editions and feature values are platform-scoped throughout, and deliberately so: a tenant that
/// could write its own entitlements would simply switch on whatever its plan withholds, which is
/// self-service licensing rather than administration. A tenant administrator's legitimate question -
/// what does my plan give me - is answered by the account's own features endpoint, which requires no
/// permission at all. None of these declare a required feature either, or a feature switched off
/// could never be switched back on.
/// </para>
/// </remarks>
public class TenancyPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Tenancy";

    public void Define(PermissionDefinitionContext context)
    {
        // Declared platform-scoped at the group, so a lifecycle permission added later inherits the
        // scope rather than silently landing in the tenant tier. Detail states its own.
        var tenantsPermissions = context.AddPermission("Tenants", "Tenants", PermissionScope.Platform);
        tenantsPermissions.AddChild(Allow.Tenant_View, "View");
        tenantsPermissions.AddChild(Allow.Tenant_Detail, "Detail", PermissionScope.Both);
        tenantsPermissions.AddChild(Allow.Tenant_Create, "Create");
        tenantsPermissions.AddChild(Allow.Tenant_Update, "Update");
        tenantsPermissions.AddChild(Allow.Tenant_Suspend, "Suspend");
        tenantsPermissions.AddChild(Allow.Tenant_Reactivate, "Reactivate");
        tenantsPermissions.AddChild(Allow.Tenant_Delete, "Delete");

        var tenantMembersPermissions = context.AddPermission("TenantMembers", "Tenant Members", PermissionScope.Both);
        tenantMembersPermissions.AddChild(Allow.TenantMember_View, "View");
        tenantMembersPermissions.AddChild(Allow.TenantMember_Add, "Add");
        tenantMembersPermissions.AddChild(Allow.TenantMember_UpdateRoles, "Update Roles");
        tenantMembersPermissions.AddChild(Allow.TenantMember_Remove, "Remove");

        var editionsPermissions = context.AddPermission("Editions", "Editions", PermissionScope.Platform);
        editionsPermissions.AddChild(Allow.Edition_View, "View");
        editionsPermissions.AddChild(Allow.Edition_Create, "Create");
        editionsPermissions.AddChild(Allow.Edition_Update, "Update");
        editionsPermissions.AddChild(Allow.Edition_Delete, "Delete");

        var featureValuePermissions = context.AddPermission("FeatureValues", "Feature Values", PermissionScope.Platform);
        featureValuePermissions.AddChild(Allow.FeatureValue_View, "View");
        featureValuePermissions.AddChild(Allow.FeatureValue_Manage, "Manage");
    }
}
