/**
 * Central registry of permission keys used throughout the app to gate access to user, role, file and tenancy features.
 * Every entry mirrors a constant of the same name in the API's `Allow` catalog.
 */
export const Allow = {
  User_View: 'User.View',
  User_Create: 'User.Create',
  User_Update: 'User.Update',
  User_Delete: 'User.Delete',

  Role_View: 'Role.View',
  Role_Create: 'Role.Create',
  Role_Update: 'Role.Update',
  Role_Delete: 'Role.Delete',
  Role_ChangePermissions: 'Role.ChangePermissions',

  File_Delete: 'File.Delete',

  Tenant_View: 'Tenant.View',
  Tenant_Detail: 'Tenant.Detail',
  Tenant_Create: 'Tenant.Create',
  Tenant_Update: 'Tenant.Update',
  Tenant_Suspend: 'Tenant.Suspend',
  Tenant_Reactivate: 'Tenant.Reactivate',
  Tenant_Delete: 'Tenant.Delete',

  TenantMember_View: 'TenantMember.View',
  TenantMember_Add: 'TenantMember.Add',
  TenantMember_UpdateRoles: 'TenantMember.UpdateRoles',
  TenantMember_Remove: 'TenantMember.Remove',

  Platform_Administration: 'Platform.Administration',
} as const
