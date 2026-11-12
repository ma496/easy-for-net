import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import * as leaveSignedOutModule from './leave-signed-out'

/** The web root, so a screen can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('..', import.meta.url))

describe('leave-signed-out', () => {
  it('exports nothing but signing out', () => {
    expect(Object.keys(leaveSignedOutModule)).toEqual(['leaveSignedOut'])
  })
})

/** A screen that ends the session. */
const signedOutSites = ['components/custom/nav-user.tsx', 'app/[lang]/(account)/account/change-password/_components/change-password-form.tsx']

describe('the screens that end the session', () => {
  it.each(signedOutSites)('%s leaves through a full page load when the session ends', (file) => {
    const source = readFileSync(join(webDirectory, file), 'utf8')

    // A fresh document is what discards the whole store; a client-side push would keep the previous
    // user's records, and resetting the cache in place would refetch them under the mounted page.
    expect(source).toContain("from '@/store/leave-signed-out'")
    expect(source).toContain('leaveSignedOut(')
    expect(source).not.toContain("router.push('/signin')")
  })
})
