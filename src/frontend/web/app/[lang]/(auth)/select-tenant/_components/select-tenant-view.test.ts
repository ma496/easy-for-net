import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/**
 * Tests what the tenant picker (`/select-tenant`) is for once each host keeps its own session: a caller whose session
 * names no tenant is sent here, is offered the tenants that are still theirs as links to each one's own
 * address, and is told plainly when there are none - or when this installation gives tenants no
 * address at all - rather than shown an empty list.
 *
 * The chain runs across two files and there is no browser here to render either of them in, so it is
 * read from the sources. What has to hold is that the guard sends such a caller here at all, that the
 * screen links rather than switches, and that the screen's own messages are defined in every locale. A
 * message missing from one locale would show a raw key to the person reading it while every other test
 * stayed green.
 */

/** The web root, so each file of the chain can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('../../../../../', import.meta.url))

/** Where the backend ships its localization resource files. */
const resourcesDirectory = fileURLToPath(new URL('../../../../../../../backend/Source/Features/Localization/Core/Resources', import.meta.url))

/** The root guard that decides where a caller with no usable tenant lands. */
const guardSource = readFileSync(join(webDirectory, 'App.tsx'), 'utf8')

/** The list itself. */
const listSource = readFileSync(join(webDirectory, 'app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx'), 'utf8')

/** The sign-in form, which used to land here and no longer does: the host names the tenant. */
const signinSource = readFileSync(join(webDirectory, 'app/[lang]/(auth)/signin/_components/signin-form.tsx'), 'utf8')

/** Every leaf of a locale file as a dotted key path, which is the string `t` looks a message up by. */
const flatten = (value: Record<string, unknown>, prefix = ''): [string, string][] =>
  Object.entries(value).flatMap(([key, entry]) => {
    const path = prefix ? `${prefix}.${key}` : key

    return entry !== null && typeof entry === 'object' ? flatten(entry as Record<string, unknown>, path) : [[path, String(entry)]]
  })

/** Every locale the project ships, by code, as the messages a screen looks up in it. */
const messages = new Map(
  readdirSync(resourcesDirectory)
    .filter((name) => name.endsWith('.json'))
    .map((name) => [name.replace(/\.json$/, ''), new Map(flatten(JSON.parse(readFileSync(join(resourcesDirectory, name), 'utf8'))))]),
)

/** Every message the list asks for, so none of them can be left untranslated. */
const listKeys = [
  'page.selectTenant.title',
  'page.selectTenant.description',
  'page.selectTenant.emptyDescription',
  'page.selectTenant.emptyAction',
  'page.selectTenant.current',
  'page.selectTenant.noAddresses',
  'navigation.platformAdministration',
  'common.opensInNewTab',
]

describe('the tenant picker', () => {
  it('reads the files the chain runs through, so a moved one cannot pass this by being absent', () => {
    expect(guardSource).toContain('isWithoutUsableTenant')
    expect(listSource).toContain('SelectTenantView')
    expect(signinSource).toContain('SigninForm')
    expect([...messages.keys()].length).toBeGreaterThanOrEqual(1)
  })

  it('is where the guard sends a caller whose session names no tenant', () => {
    expect(guardSource, 'the one place this is decided, rather than each screen guessing').toContain('isWithoutUsableTenant(authState.user)')
    expect(guardSource).toContain("go('/select-tenant')")
  })

  it('is not a landing at sign-in, where the host has already named the tenant', () => {
    expect(signinSource).not.toContain('select-tenant')
    expect(signinSource).not.toContain('resolveTenantLanding')
  })

  it('offers the tenants the caller may still work in, read from the session, as links to their own addresses', () => {
    expect(listSource).toContain('state.auth.tenants')
    expect(listSource).toContain('tenant.url')
    expect(listSource).toContain('<TenantLink')
    expect(listSource).not.toContain('onClick')
  })

  it('says so when there is nothing to link to, instead of showing an empty list', () => {
    expect(listSource).toContain('tenants.length === 0')
    expect(listSource).toContain("t('page.selectTenant.emptyDescription')")
    expect(listSource).toContain("t('page.selectTenant.noAddresses')")
  })

  it.each(listKeys)('%s is defined in every locale, so no arrival shows a raw key', (key) => {
    for (const [locale, translated] of messages) {
      const message = translated.get(key)

      expect(message && message.trim().length > 0 && message !== key, `${locale} would show the key itself`).toBe(true)
    }
  })
})
