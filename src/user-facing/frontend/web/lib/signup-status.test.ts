import { describe, expect, it } from 'vitest'
import { homeCtas, isSignupEnabledFrom } from './signup-status'

describe('isSignupEnabledFrom', () => {
  it('is closed only when the API says isSignupEnabled: false', () => {
    expect(isSignupEnabledFrom({ isSignupEnabled: false })).toBe(false)
  })

  it('is open when the API says isSignupEnabled: true', () => {
    expect(isSignupEnabledFrom({ isSignupEnabled: true })).toBe(true)
  })

  it.each([null, undefined, 'false', 0, {}, { isSignupEnabled: 'false' }, { isSignupEnabled: null }, []])('treats an unreachable API or a malformed answer (%j) as open', (body) => {
    expect(isSignupEnabledFrom(body)).toBe(true)
  })
})

describe('homeCtas', () => {
  it('offers sign-up in the hero and the closing section while sign-up is open', () => {
    const ctas = homeCtas(true)
    expect(ctas.heroPrimary).toEqual({ href: '/signup', label: 'getStarted' })
    expect(ctas.heroSecondary).toEqual({ href: '/about', label: 'learnMore' })
    expect(ctas.closing).toEqual({ href: '/signup', label: 'getStarted', invitesSignup: true })
  })

  it('links nowhere near sign-up while it is closed', () => {
    const ctas = homeCtas(false)
    expect(ctas.heroPrimary).toEqual({ href: '/signin', label: 'signIn' })
    expect(ctas.heroSecondary).toEqual({ href: '/about', label: 'learnMore' })
    expect(ctas.closing).toEqual({ href: '/about', label: 'learnMore', invitesSignup: false })
    expect(Object.values(ctas).map((cta) => cta.href)).not.toContain('/signup')
  })
})
