import { describe, expect, it } from 'vitest'
import { Allow } from '@/allow'
import { decideGuardRedirect } from './guard-redirect'

const decide = (path: string, granted: string[] | null, isSettled = true) =>
  decideGuardRedirect({
    path,
    isSettled,
    isAuthenticated: granted !== null,
    isPermitted: (permissions) => permissions.every((permission) => (granted ?? []).includes(permission)),
  })

describe('decideGuardRedirect', () => {
  it('decides nothing until the account info has answered', () => {
    expect(decide('/admin/users', null, false)).toBeNull()
  })

  it('sends a visitor without a session to sign in, returning to the screen afterwards', () => {
    expect(decide('/admin/users', null)).toBe('/signin?redirect=%2Fadmin%2Fusers')
    expect(decide('/account/profile', null)).toBe('/signin?redirect=%2Faccount%2Fprofile')
  })

  it('lets a signed-in visitor open their own account screens', () => {
    expect(decide('/account/profile', [])).toBeNull()
  })

  it('keeps an account without Admin.Access out of the whole admin area', () => {
    expect(decide('/admin', [])).toBe('/unauthorized')
    expect(decide('/admin/users', [Allow.User_View])).toBe('/unauthorized')
    expect(decide('/admin/notifications', [])).toBe('/unauthorized')
  })

  it('also asks for the screen permission', () => {
    expect(decide('/admin/seo', [Allow.Admin_Access])).toBe('/unauthorized')
    expect(decide('/admin/seo', [Allow.Admin_Access, Allow.Seo_View])).toBeNull()
    expect(decide('/admin', [Allow.Admin_Access])).toBeNull()
  })
})
