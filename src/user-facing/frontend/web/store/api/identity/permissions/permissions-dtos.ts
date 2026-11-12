/** Response shape for the permission-definition endpoint, returning the catalog of permission groups. */
export interface PermissionDefinitionResponse {
  groups: PermissionGroupDefinition[]
}

/** Logical group of permission definitions in the permission catalog. */
export interface PermissionGroupDefinition {
  groupName: string
  permissions: PermissionDefinition[]
}

/** Tree node representing a permission and any nested child permissions within the catalog. */
export interface PermissionDefinition {
  name: string
  displayName: string
  children: PermissionDefinition[]
}

/** Response shape for the get-permissions endpoint, returning the flat list of assigned permissions. */
export interface PermissionResponse {
  permissions: PermissionDto[]
}

/** Lightweight permission descriptor (id, name, displayName) used in role/user payloads. */
export interface PermissionDto {
  id: string
  name: string
  displayName: string
}
