import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import * as tenantCache from './tenant-cache'

/**
 * Each host keeps its own session, so the acting tenant is never changed in place: another tenant
 * is another address, and the API refuses `/tenants/switch` and `/tenants/exit` on a resolved host.
 * What is asserted here is that the web app has no path left that would call them - and it has to be
 * read from the source, because there is no browser here to render a screen in. A screen that quietly
 * switched in place again would show the previous tenant's records under the new one's session, and
 * every other test would stay green while it did.
 */

/** The web root, so a screen can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('..', import.meta.url))

/** Directories that are not the app's own source. */
const skippedDirectories = new Set(['node_modules', 'public', 'coverage'])

/** Every source file of the web app, tests excluded (this one names what it forbids). */
const sourceFiles = (relativeDirectory = ''): string[] =>
  readdirSync(join(webDirectory, relativeDirectory), { withFileTypes: true }).flatMap((entry) => {
    const relative = relativeDirectory ? `${relativeDirectory}/${entry.name}` : entry.name
    if (entry.isDirectory()) return skippedDirectories.has(entry.name) || entry.name.startsWith('.') ? [] : sourceFiles(relative)
    return /\.(ts|tsx|mjs)$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) && !entry.name.endsWith('.d.ts') ? [relative] : []
  })

const sources = sourceFiles().map((file) => ({ file, text: readFileSync(join(webDirectory, file), 'utf8') }))

describe('changing the acting tenant', () => {
  it('reads the source it is meant to be checking, so a moved tree cannot pass this by being absent', () => {
    expect(sources.length).toBeGreaterThan(100)
    expect(sources.some(({ file }) => file === 'components/custom/nav-user.tsx')).toBe(true)
  })

  it.each(['/tenants/switch', '/tenants/exit'])('is never done in place: nothing in the web app calls %s', (endpoint) => {
    expect(sources.filter(({ text }) => text.includes(endpoint)).map(({ file }) => file)).toEqual([])
  })

  it('keeps no machinery for a switch in place', () => {
    const leftovers = ['useTenantSwitch', 'TenantSwitcher', 'useTenantSwitchMutation', 'useTenantExitMutation', 'leaveForTenantChange', 'takeTenantChangeNotice', 'TenantChangeNotice']
    const offenders = sources.flatMap(({ file, text }) => leftovers.filter((name) => text.includes(name)).map((name) => `${file} still names ${name}`))

    expect(offenders).toEqual([])
  })

  it('calls a tenant a tenant: no source file says workspace', () => {
    expect(sources.filter(({ text }) => /workspace/i.test(text)).map(({ file }) => file)).toEqual([])
  })

  it('leaves the session store with nothing but signing out', () => {
    expect(Object.keys(tenantCache)).toEqual(['leaveSignedOut'])
  })
})

/** The screens that link to the account's other tenants. */
const tenantLinkSites = [
  'components/custom/nav-user.tsx',
  'app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx',
  'app/[lang]/admin/(tenancy)/tenants/_components/tenant-table.tsx',
]

describe('links to another tenant', () => {
  it.each(tenantLinkSites)("%s links to the address the account info reports, never one of its own", (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    // `tenant.url` (and `platformUrl`) are the only source of a tenant's address: a composed or
    // baked-in one would point at the wrong host the moment the deployment's domain differs.
    expect(source).toMatch(/\.url\b/)
    expect(source).not.toMatch(/https?:\/\//)
    expect(source).not.toContain('localhost')
  })

  it.each(tenantLinkSites)('%s opens it as another site rather than an app route', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    expect(source.includes('TenantLink') || source.includes('external: true') || source.includes('useOpenTenant')).toBe(true)
  })

  it('opens a tenant signed in through the address the API issues the ticket for, handing the ticket to that tab alone', () => {
    const source = readFileSync(join(webDirectory, 'hooks/use-open-tenant.ts'), 'utf8')

    expect(source).toContain('response.data')
    expect(source).toContain("window.open('', '_blank')")
    // The ticket goes to the tab this page opened, on the tenant's origin, and never into an address.
    expect(source).toContain('event.source !== tab')
    expect(source).toContain('event.origin !== tenantOrigin')
    expect(source).toContain('tab.postMessage({ type: TICKET_MESSAGE.ticket, ticket }, tenantOrigin)')
    expect(source).not.toContain('#ticket')
    expect(source).not.toMatch(/https?:\/\//)
  })

  it("takes the ticket only from the page that opened the tab, on one of the deployment's own origins", () => {
    const source = readFileSync(join(webDirectory, 'app/[lang]/(auth)/ticket-signin/_components/ticket-signin-view.tsx'), 'utf8')

    expect(source).toContain('event.source !== opener || !isTrustedSender(event.origin)')
    expect(source).toContain('isDeploymentOrigin(origin, { platformUrl, tenantUrlTemplate })')
    expect(source).toContain('await isServedOrigin(received.origin)')
    expect(source).not.toContain('location.hash')
  })

  it('opens a tenant in a new tab, with nothing of this page handed to it', () => {
    const source = readFileSync(join(webDirectory, 'components/custom/tenant-link.tsx'), 'utf8')

    expect(source).toContain('target="_blank"')
    expect(source).toContain('rel="noopener noreferrer"')
    expect(source).not.toMatch(/<LocalizedLink\b/)
  })
})

/** A screen that ends the session, after which no tenant stands to cache anything for. */
const signedOutSites = [
  'components/custom/nav-user.tsx',
  'app/[lang]/(auth)/change-password/_components/change-password-form.tsx',
]

describe('the screens that end the session', () => {
  it.each(signedOutSites)('%s leaves through a full page load when the session ends', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    // A fresh document is what discards the whole store; a client-side push would keep the previous
    // tenant's records, and resetting the cache in place would refetch them under the mounted page.
    expect(source).toContain("from '@/store/tenant-cache'")
    expect(source).toContain('leaveSignedOut(')
    expect(source).not.toContain("router.push('/signin')")
  })
})
