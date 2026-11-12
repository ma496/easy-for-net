import { describe, expect, it } from 'vitest'
import { Allow } from './allow'

/**
 * The API's `Allow` catalog, restated here as the client must name it. The pair is only useful if the
 * two sides agree: a permission renamed on one side alone is either a screen nobody can open or a gate
 * that never closes, and neither shows up as a compile error.
 */
const apiPermissions = {
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

describe('Allow', () => {
  it.each(Object.entries(apiPermissions))('mirrors %s as %s', (name, value) => {
    expect(Allow[name as keyof typeof apiPermissions]).toBe(value)
  })

  it('carries every permission the API enforces, and no others', () => {
    expect(Object.keys(Allow).sort()).toEqual(Object.keys(apiPermissions).sort())
  })

  it('gives no two permissions the same value', () => {
    const values = Object.values(Allow)

    expect(new Set(values).size).toBe(values.length)
  })

  it('names every permission as the group and the operation it belongs to', () => {
    for (const value of Object.values(Allow)) {
      expect(value).toMatch(/^[A-Z][A-Za-z]*\.[A-Z][A-Za-z]*$/)
    }
  })
})
