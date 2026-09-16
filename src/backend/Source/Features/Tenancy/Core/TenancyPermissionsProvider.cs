namespace Backend.Features.Tenancy.Core;

/// <summary>
/// Declares the permission hierarchy for the Tenancy feature: the tenant lifecycle, tenant
/// membership and platform administration.
/// </summary>
/// <remarks>
/// The catalogue is declared here in code, so it is global, identical for every tenant and closed to
/// extension at run time. Leaves marked platform-tier can never be granted through a tenant role:
/// creating, renaming, suspending, reactivating and deleting a tenant, along with platform
/// administration itself, stay with the platform administrator - who is also the caller that
/// platform administration widens to every tenant, and the only caller admitted to platform-wide
/// surfaces such as the background-job dashboard. Viewing a tenant and managing its members are
/// tenant-tier, so they are offered on the role-permission surface and belong to the system-created
/// administrator role provisioned with each new tenant.
/// </remarks>
public class TenancyPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Tenancy";

    public void Define(PermissionDefinitionContext context)
    {
        var tenantsPermissions = context.AddPermission("Tenants", "Tenants");
        tenantsPermissions.AddChild(Allow.Tenant_View, "View");
        tenantsPermissions.AddChild(Allow.Tenant_Create, "Create", isPlatform: true);
        tenantsPermissions.AddChild(Allow.Tenant_Update, "Update", isPlatform: true);
        tenantsPermissions.AddChild(Allow.Tenant_Suspend, "Suspend", isPlatform: true);
        tenantsPermissions.AddChild(Allow.Tenant_Reactivate, "Reactivate", isPlatform: true);
        tenantsPermissions.AddChild(Allow.Tenant_Delete, "Delete", isPlatform: true);

        var tenantMembersPermissions = context.AddPermission("TenantMembers", "Tenant Members");
        tenantMembersPermissions.AddChild(Allow.TenantMember_View, "View");
        tenantMembersPermissions.AddChild(Allow.TenantMember_Add, "Add");
        tenantMembersPermissions.AddChild(Allow.TenantMember_UpdateRoles, "UpdateRoles");
        tenantMembersPermissions.AddChild(Allow.TenantMember_Remove, "Remove");

        var platformPermissions = context.AddPermission("Platform", "Platform");
        platformPermissions.AddChild(Allow.Platform_Administration, "Administration", isPlatform: true);
    }
}
