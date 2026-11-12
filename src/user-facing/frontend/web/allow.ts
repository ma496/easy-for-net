/**
 * Central registry of permission keys used throughout the app to gate access to the admin area and its screens.
 * Every entry mirrors a constant of the same name in the API's `Allow` catalog.
 */
export const Allow = {
  Admin_Access: 'Admin.Access',

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

  Localization_View: 'Localization.View',
  Localization_Update: 'Localization.Update',

  Settings_View: 'Settings.View',
  Settings_Update: 'Settings.Update',

  Seo_View: 'Seo.View',
  Seo_Update: 'Seo.Update',

  Hangfire_Dashboard: 'Hangfire.Dashboard',
} as const
