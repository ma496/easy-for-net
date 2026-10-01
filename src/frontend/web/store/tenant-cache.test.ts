import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { leaveForTenantChange, takeTenantChangeNotice } from './tenant-cache'

/** A session storage the way a browser tab keeps one, so the notice can be read back across the "load". */
const createStorage = () => {
  const items = new Map<string, string>()
  return {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
    removeItem: (key: string) => void items.delete(key),
  }
}

describe('leaving for a tenant change', () => {
  let replace: ReturnType<typeof vi.fn>
  let sessionStorage: ReturnType<typeof createStorage>

  beforeEach(() => {
    replace = vi.fn()
    sessionStorage = createStorage()
    vi.stubGlobal('window', { location: { replace }, sessionStorage })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('replaces the document, so nothing held for the scope just left survives and Back skips the page', () => {
    leaveForTenantChange('/admin', { messageKey: 'page.tenants.switcher.switchSuccess', tenant: 'Tenant b' })

    expect(replace).toHaveBeenCalledWith('/admin')
  })

  it('leaves the success message for the page it lands on, which shows it once', () => {
    leaveForTenantChange('/admin', { messageKey: 'page.tenants.switcher.switchSuccess', tenant: 'Tenant b' })

    expect(takeTenantChangeNotice()).toEqual({ messageKey: 'page.tenants.switcher.switchSuccess', tenant: 'Tenant b' })
    expect(takeTenantChangeNotice()).toBeNull()
  })

  it('announces nothing on a load no change led to', () => {
    expect(takeTenantChangeNotice()).toBeNull()
  })

  it('ignores a stored value that is not a notice', () => {
    sessionStorage.setItem('tenant-change-notice', '{"messageKey":1}')
    expect(takeTenantChangeNotice()).toBeNull()

    sessionStorage.setItem('tenant-change-notice', 'not json')
    expect(takeTenantChangeNotice()).toBeNull()
  })

  it('still lands when the browser refuses session storage', () => {
    vi.stubGlobal('window', {
      location: { replace },
      sessionStorage: {
        setItem: () => {
          throw new Error('blocked')
        },
      },
    })

    leaveForTenantChange('/admin', { messageKey: 'page.tenants.switcher.exitSuccess', tenant: '' })

    expect(replace).toHaveBeenCalledWith('/admin')
  })
})

/**
 * What is asserted here is that the screens call the helpers, not that the helpers work - and it has to be
 * read from the source, because there is no browser here to render a screen in. The gap it closes is the
 * one that matters for the criterion: a screen that quietly stopped leaving through a fresh document would
 * leave a record of the previous tenant on display, and every other test in this file would stay green while it
 * did.
 */

/** The web root, so a screen can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('..', import.meta.url))

/**
 * A screen that changes which tenant the session acts in. Each reaches it through `useTenantSwitch`,
 * which is the one place entering and leaving a tenant is carried out.
 */
const tenantChangedSites = [
  'components/custom/tenant-switcher.tsx',
  'app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx',
  'app/[lang]/admin/(tenancy)/tenants/_components/tenant-table.tsx',
]

/** The hook every screen above changes the acting tenant through. */
const tenantSwitchHook = 'hooks/use-tenant-switch.ts'

/** A screen that ends the session, after which no tenant stands to cache anything for. */
const signedOutSites = [
  'components/custom/nav-user.tsx',
  'app/[lang]/(auth)/change-password/_components/change-password-form.tsx',
]

describe('the screens that change or end the tenant', () => {
  it.each(tenantChangedSites)('%s changes the tenant through the hook', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    // Driving the hook is the only way to the full page load that discards the previous tenant's
    // records; a screen that changed the tenant some other way would keep them on display.
    expect(source).toContain('useTenantSwitch')
  })

  it('changes the acting tenant in one place, which lands through a full page load', () => {
    const source = readFileSync(join(webDirectory, tenantSwitchHook), 'utf8')

    // Resetting the cache in place would refetch every query of the still-mounted page under the new
    // session, and a page the new scope may not read would show its 403 until the navigation landed.
    expect(source).toContain("from '@/store/tenant-cache'")
    expect(source).toContain('leaveForTenantChange(')
    expect(source).not.toContain('resetApiState')
    expect(source).not.toContain('router.push(')
  })

  it.each(signedOutSites)('%s leaves through a full page load when the session ends', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    // A fresh document is what discards the whole store; a client-side push would keep the previous
    // tenant's records, and resetting the cache in place would refetch them under the mounted page.
    expect(source).toContain("from '@/store/tenant-cache'")
    expect(source).toContain('leaveSignedOut(')
    expect(source).not.toContain("router.push('/signin')")
  })
})
