import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/**
 * Tests what the tenant chooser is for once a session can lose its tenant but never be refused over
 * it: a caller whose session names no tenant is sent here, is offered the tenants that are still
 * theirs, and is told plainly when there are none rather than shown an empty list.
 *
 * The chain runs across two files and there is no browser here to render either of them in, so it is
 * read from the sources. What has to hold is that the guard sends such a caller here at all, and that
 * the screen's own messages - including the one it shows when nothing is left to choose - are defined
 * in every locale. A message missing from one locale would show a raw key to the person reading it
 * while every other test stayed green.
 */

/** The web root, so each file of the chain can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('../../../../../', import.meta.url))

/** The root guard that decides where a caller with no usable tenant lands. */
const guardSource = readFileSync(join(webDirectory, 'App.tsx'), 'utf8')

/** The chooser itself. */
const chooserSource = readFileSync(
  join(webDirectory, 'app/[lang]/(auth)/select-tenant/_components/select-tenant-view.tsx'),
  'utf8'
)

/** Every leaf of a locale file as a dotted key path, which is the string `t` looks a message up by. */
const flatten = (value: Record<string, unknown>, prefix = ''): [string, string][] =>
  Object.entries(value).flatMap(([key, entry]) => {
    const path = prefix ? `${prefix}.${key}` : key

    return entry !== null && typeof entry === 'object'
      ? flatten(entry as Record<string, unknown>, path)
      : [[path, String(entry)]]
  })

/** Every locale the project ships, by code, as the messages a screen looks up in it. */
const messages = new Map(
  readdirSync(join(webDirectory, 'public/locales'))
    .filter((name) => name.endsWith('.json'))
    .map((name) => [
      name.replace(/\.json$/, ''),
      new Map(flatten(JSON.parse(readFileSync(join(webDirectory, 'public/locales', name), 'utf8')))),
    ])
)

/** Every message the chooser asks for, so none of them can be left untranslated. */
const chooserKeys = [
  'page.selectTenant.title',
  'page.selectTenant.description',
  'page.selectTenant.emptyDescription',
  'page.selectTenant.emptyAction',
]

describe('the tenant chooser', () => {
  it('reads the files the chain runs through, so a moved one cannot pass this by being absent', () => {
    expect(guardSource).toContain('resolveTenantLanding')
    expect(chooserSource).toContain('SelectTenantView')
    expect([...messages.keys()].length).toBeGreaterThanOrEqual(1)
  })

  it('is where the guard sends a caller whose session names no tenant', () => {
    expect(
      guardSource,
      'the landing decision is the one place this is decided, rather than each screen guessing'
    ).toContain('resolveTenantLanding(authState.user)')
  })

  it('offers the tenants the caller may still work in, read from the session rather than the URL', () => {
    expect(chooserSource).toContain('state.auth.tenants')
    expect(chooserSource).toContain('enterTenant(')
  })

  it('says so when there is nothing left to choose, instead of showing an empty list', () => {
    expect(chooserSource).toContain('tenants.length === 0')
    expect(chooserSource).toContain("t('page.selectTenant.emptyDescription')")
  })

  it.each(chooserKeys)('%s is defined in every locale, so no arrival shows a raw key', (key) => {
    for (const [locale, translated] of messages) {
      const message = translated.get(key)

      expect(
        message && message.trim().length > 0 && message !== key,
        `${locale} would show the key itself`
      ).toBe(true)
    }
  })
})
