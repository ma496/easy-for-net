import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { beforeEach, describe, expect, it } from 'vitest'
import { appApi } from '@/store/api/_app-api'
import { store } from '@/store'
import { setUnreadCount, setUserInfo, signout } from '@/store/slices'
// A value import, so the account endpoints this file puts in the cache are registered on appApi.
import { accountApi } from '@/store/api/identity'
import type { GetUserInfoResponse } from '@/store/api/identity'
import { dispatchSignedOut, dispatchTenantChanged, signedOutActions, tenantChangedActions } from './tenant-cache'

const resetApiState = appApi.util.resetApiState()

/** The account info a switch or an onboard answers with: the session re-established in the new tenant. */
const userInfo: GetUserInfoResponse = {
  id: 'user-1',
  username: 'someone',
  email: 'someone@example.com',
  isPlatform: false,
  activeTenantId: 'b',
  activeTenant: { id: 'b', name: 'Tenant b', identifier: 't-b' },
  tenants: [
    { id: 'a', name: 'Tenant a', identifier: 't-a' },
    { id: 'b', name: 'Tenant b', identifier: 't-b' },
  ],
  roles: [],
}

describe('tenantChangedActions', () => {
  it('discards the tenant-scoped cache before it puts the new tenant in place', () => {
    const actions = tenantChangedActions(userInfo)

    expect(actions).toEqual([resetApiState, setUserInfo(userInfo), setUnreadCount(0)])
  })

  it('resets the cache first, so nothing cached for the previous tenant outlives the change', () => {
    const actions = tenantChangedActions(userInfo)

    expect(actions[0].type).toBe(resetApiState.type)
    expect(actions[1].type).toBe(setUserInfo(userInfo).type)
    expect(actions[2].type).toBe(setUnreadCount(0).type)
  })

  it('carries the freshly read account info, so the new tenant is the one that stands', () => {
    const [, carried] = tenantChangedActions(userInfo)

    expect(carried).toEqual(setUserInfo(userInfo))
    expect((carried as ReturnType<typeof setUserInfo>).payload?.activeTenant?.id).toBe('b')
  })

  it('starts the unread badge again from zero', () => {
    const [, , unread] = tenantChangedActions(userInfo)

    expect(unread).toEqual(setUnreadCount(0))
  })
})

describe('signedOutActions', () => {
  it('discards the cache, clears the selection and the badge when the user signs out', () => {
    const actions = signedOutActions()

    expect(actions).toEqual([resetApiState, signout(), setUnreadCount(0)])
  })

  it('resets the cache first, so no previous tenant record survives the sign-out', () => {
    const actions = signedOutActions()

    expect(actions[0].type).toBe(resetApiState.type)
    expect(actions[1].type).toBe(signout().type)
    expect(actions[2].type).toBe(setUnreadCount(0).type)
  })
})

/**
 * The dispatch helpers are what every screen that changes or ends the tenant actually calls, so the
 * cases below drive them through the application's own store and then read the store back: what the
 * browser still holds for the tenant just left is what the assertion is about, not the shape of the
 * action list that was dispatched at it.
 */
describe('the dispatch helpers', () => {
  /** The queries the browser is currently holding a cached record for. */
  const cachedQueries = (): string[] => Object.keys(store.getState().appApi.queries)

  /** Reads a record into the cache, as a screen that loaded it in this tenant would have. */
  const cacheARecord = async (tenantId: string): Promise<void> => {
    // Seeded through the injected api the account endpoints were declared on, because that is where
    // `getUserInfo` is typed: `injectEndpoints` returns the widened api rather than widening `appApi`.
    // It is the same instance - one reducerPath, one middleware, one store - so what this writes is
    // the very cache the assertions below read back.
    await store.dispatch(
      accountApi.util.upsertQueryData('getUserInfo', undefined, { ...userInfo, activeTenantId: tenantId })
    )
  }

  beforeEach(() => {
    store.dispatch(appApi.util.resetApiState())
  })

  it('leaves the previous tenant no cached record once the active tenant has changed', async () => {
    await cacheARecord('a')
    expect(cachedQueries(), 'the cache has to hold something for the reset to be provable').toHaveLength(1)

    dispatchTenantChanged(store.dispatch, userInfo)

    expect(cachedQueries()).toEqual([])
  })

  it('puts the freshly read tenant in the session state, so the selection is the new one', async () => {
    dispatchTenantChanged(store.dispatch, userInfo)

    expect(store.getState().auth.activeTenant?.id).toBe('b')
    expect(store.getState().auth.tenants).toEqual(userInfo.tenants)
  })

  it('leaves no cached record and no selection behind once the user has signed out', async () => {
    await cacheARecord('a')
    store.dispatch(setUserInfo(userInfo))
    expect(cachedQueries()).toHaveLength(1)

    dispatchSignedOut(store.dispatch)

    expect(cachedQueries()).toEqual([])
    expect(store.getState().auth.isAuthenticated).toBe(false)
    expect(store.getState().auth.activeTenant).toBeUndefined()
  })

  it('zeroes the unread badge, which lives in slice state and would survive the cache reset', async () => {
    store.dispatch(setUnreadCount(7))

    dispatchSignedOut(store.dispatch)

    expect(store.getState().notifications.unreadCount).toBe(0)
  })
})

/**
 * What is asserted here is that the screens call the helpers, not that the helpers work - and it has to be
 * read from the source, because there is no browser here to render a screen in. The gap it closes is the
 * one that matters for the criterion: a screen that quietly stopped resetting the cache would leave a
 * record of the previous tenant on display, and every other test in this file would stay green while it
 * did (AC-028).
 */

/** The web root, so a screen can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('..', import.meta.url))

/**
 * A screen that changes which tenant the session acts in. Most of them reach the reset through
 * `useTenantSwitch`, which is the one place entering and leaving a tenant is carried out.
 */
const tenantChangedSites = [
  'components/custom/tenant-switcher.tsx',
  'app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx',
  'app/[lang]/admin/(tenancy)/tenants/list/_components/tenant-table.tsx',
]

/** The hook every screen above changes the acting tenant through. */
const tenantSwitchHook = 'hooks/use-tenant-switch.ts'

/** A screen that ends the session, after which no tenant stands to cache anything for. */
const signedOutSites = [
  'components/custom/nav-user.tsx',
  'app/[lang]/(auth)/change-password/_components/change-password-form.tsx',
]

describe('the screens that change or end the tenant', () => {
  it.each(tenantChangedSites)('%s resets the cache when the tenant changes', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    // Either way of reaching the reset counts, and nothing else does: a screen that dispatched the
    // sequence itself and a screen that drives the hook both leave nothing of the previous tenant on
    // display, while a screen that has quietly stopped doing either fails here.
    const dispatchesItself = source.includes("from '@/store/tenant-cache'") && source.includes('dispatchTenantChanged(dispatch')
    const goesThroughTheHook = source.includes('useTenantSwitch')

    expect(dispatchesItself || goesThroughTheHook).toBe(true)
  })

  it('changes the acting tenant in one place, which resets the cache through the helper', () => {
    const source = readFileSync(join(webDirectory, tenantSwitchHook), 'utf8')

    expect(source).toContain("from '@/store/tenant-cache'")
    expect(source).toContain('dispatchTenantChanged(dispatch')
    expect(source).toContain('appApi.util.resetApiState()')
  })

  it.each(signedOutSites)('%s resets the cache through the helper when the session ends', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    expect(source).toContain("from '@/store/tenant-cache'")
    expect(source).toContain('dispatchSignedOut(dispatch')
  })
})
