import { describe, expect, it } from 'vitest'
import { pathOnOrigin, tenantUrlFromTemplate, workspaceSigninUrl } from './workspace-url'

describe('tenantUrlFromTemplate', () => {
  it('puts the identifier in place of the placeholder', () => {
    expect(tenantUrlFromTemplate('http://{tenant}.localhost:3000', 'acme')).toBe('http://acme.localhost:3000')
    expect(tenantUrlFromTemplate('https://{tenant}.example.com', 'acme-labs')).toBe('https://acme-labs.example.com')
  })

  it('trims and lower-cases the identifier, as the API normalizes it', () => {
    expect(tenantUrlFromTemplate('https://{tenant}.example.com', '  Acme ')).toBe('https://acme.example.com')
  })

  it.each([null, undefined, ''])('gives no address when there is no template (%s)', (template) => {
    expect(tenantUrlFromTemplate(template, 'acme')).toBeNull()
  })

  it('gives no address for a template without the placeholder', () => {
    expect(tenantUrlFromTemplate('https://example.com', 'acme')).toBeNull()
  })

  it.each([null, undefined, '', '   '])('gives no address when there is no identifier (%s)', (identifier) => {
    expect(tenantUrlFromTemplate('https://{tenant}.example.com', identifier)).toBeNull()
  })
})

describe('pathOnOrigin', () => {
  it('joins with exactly one slash', () => {
    expect(pathOnOrigin('http://app.localhost:3000', '/signin')).toBe('http://app.localhost:3000/signin')
    expect(pathOnOrigin('http://app.localhost:3000/', '/ar/signin')).toBe('http://app.localhost:3000/ar/signin')
    expect(pathOnOrigin('http://app.localhost:3000', 'signup')).toBe('http://app.localhost:3000/signup')
  })
})

describe('workspaceSigninUrl', () => {
  it("links to the new workspace's own sign-in page when the template gives it an address", () => {
    expect(
      workspaceSigninUrl({ tenantUrlTemplate: 'http://{tenant}.localhost:3000', platformUrl: 'http://app.localhost:3000', identifier: 'acme' }, '/fr/signin'),
    ).toBe('http://acme.localhost:3000/fr/signin')
  })

  it("falls back to the platform's sign-in page while host resolution is off", () => {
    expect(workspaceSigninUrl({ tenantUrlTemplate: null, platformUrl: 'http://localhost:3000', identifier: 'acme' }, '/signin')).toBe('http://localhost:3000/signin')
  })
})
