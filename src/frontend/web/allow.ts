/**
 * Central registry of permission keys used throughout the app to gate access to user and role management features.
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
} as const
