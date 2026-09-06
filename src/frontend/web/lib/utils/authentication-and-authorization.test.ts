import { describe, expect, it } from 'vitest'
import { isAllowed, type AuthState } from './authentication-and-authorization'

const stateWithPermissions = (...permissions: string[]): AuthState => ({
  isAuthenticated: true,
  user: {
    id: 'user-id',
    username: 'user',
    email: 'user@example.com',
    roles: [
      {
        id: 'role-id',
        name: 'Admin',
        permissions: permissions.map((name) => ({ id: name, name, displayName: name })),
      },
    ],
  },
})

describe('isAllowed', () => {
  it('requires every requested permission', () => {
    expect(isAllowed(stateWithPermissions('User.View'), ['User.View', 'User.Delete'])).toBe(false)
    expect(isAllowed(stateWithPermissions('User.View', 'User.Delete'), ['User.View', 'User.Delete'])).toBe(true)
  })

  it('allows an authenticated user when no permission is required', () => {
    expect(isAllowed(stateWithPermissions(), [])).toBe(true)
  })
})
