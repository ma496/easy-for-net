import { describe, expect, it } from 'vitest'
import { accountPath, adminPath, isAccountPath, isAdminPath } from './paths'

describe('adminPath', () => {
  it('puts a screen under the admin segment', () => {
    expect(adminPath('/users')).toBe('/admin/users')
    expect(adminPath('/users/{id}/update')).toBe('/admin/users/{id}/update')
    expect(adminPath('seo')).toBe('/admin/seo')
  })

  it('names the dashboard for the root, with no trailing slash', () => {
    expect(adminPath()).toBe('/admin')
    expect(adminPath('/')).toBe('/admin')
  })
})

describe('accountPath', () => {
  it('puts a screen under the account segment', () => {
    expect(accountPath('/profile')).toBe('/account/profile')
    expect(accountPath('/change-password')).toBe('/account/change-password')
  })
})

describe('isAdminPath / isAccountPath', () => {
  it('matches the segment and what lies beneath it', () => {
    expect(isAdminPath('/admin')).toBe(true)
    expect(isAdminPath('/admin/users?page=2')).toBe(true)
    expect(isAccountPath('/account/profile')).toBe(true)
  })

  it('does not match a path that merely starts with the same letters', () => {
    expect(isAdminPath('/administrators')).toBe(false)
    expect(isAccountPath('/accounts')).toBe(false)
    expect(isAdminPath('/')).toBe(false)
    expect(isAdminPath('/about')).toBe(false)
  })
})
