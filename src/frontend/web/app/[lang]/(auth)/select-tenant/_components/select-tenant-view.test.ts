import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { tenantRefusalReasonKey } from '@/lib/utils/tenant-routing'

/**
 * Tests the explanation the tenant chooser shows a caller who was sent there by a refusal: a session
 * whose tenant went away is offered another one, and the screen has to say what happened rather than
 * leaving them staring at a failure (AC-070).
 *
 * The chain being checked runs across three files and there is no browser here to render any of them
 * in, so it is read from the sources. What has to hold is that the code the error middleware records
 * is the code the root guard carries in the query string, and that the chooser can turn it into a
 * message every locale defines: a code that arrives with no explanation would either show a raw key
 * to the person reading it or silently explain nothing, and both would leave the criterion unmet
 * while the middleware's own tests stayed green.
 */

/** The web root, so each file of the chain can be read by the path it is written under. */
const webDirectory = fileURLToPath(new URL('../../../../../', import.meta.url))

/** The middleware that decides a refusal is one the app reacts to, and records which. */
const middlewareSource = readFileSync(join(webDirectory, 'store/middlewares/rtk-error-middleware.ts'), 'utf8')

/** The root guard that carries the recorded code to the chooser. */
const guardSource = readFileSync(join(webDirectory, 'App.tsx'), 'utf8')

/** The chooser itself, which renders the banner. */
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

/**
 * The ways a session's tenant can stop being usable, each of which has an explanation of its own on the
 * chooser. The middleware is asserted to hold exactly these below, so the list cannot drift into
 * describing codes nothing records.
 */
const tenantRefusalCodes = [
  'tenantSuspended',
  'tenantMembershipRevoked',
  'tenantNotFound',
  'noActiveTenant',
  'notTenantMember',
]

describe('the chooser explanation banner', () => {
  it('reads the files the chain runs through, so a moved one cannot pass this by being absent', () => {
    expect(middlewareSource).toContain('rtkErrorMiddleware')
    expect(guardSource).toContain('resolveTenantLanding')
    expect(chooserSource).toContain('SelectTenantView')
    expect([...messages.keys()].length).toBeGreaterThanOrEqual(1)
  })

  it.each(tenantRefusalCodes)('%s is a code the error middleware records', (code) => {
    expect(
      middlewareSource,
      'the code is one the middleware turns into a tenant error the app reacts to'
    ).toContain(`'${code}'`)
  })

  it.each(tenantRefusalCodes)('%s is explained on the chooser', (code) => {
    const key = tenantRefusalReasonKey(code)

    expect(key, 'a refusal the app reacts to has to be one the chooser can explain').toBeDefined()
    expect(key, 'the explanation is a message of the chooser rather than of another screen').toMatch(
      /^page\.selectTenant\./
    )
  })

  it('explains every code the middleware records in every locale, so no arrival shows a raw key', () => {
    for (const [locale, translated] of messages) {
      const unexplained = tenantRefusalCodes.filter((code) => {
        const message = translated.get(tenantRefusalReasonKey(code)!)

        return !message || message.trim().length === 0 || message === tenantRefusalReasonKey(code)
      })

      expect(unexplained, `${locale} would leave a refused caller unexplained`).toEqual([])
    }
  })

  it('reads the reason off the query string and renders it through the resolved key', () => {
    expect(
      chooserSource,
      'the reason travels as a query parameter, which survives the redirect'
    ).toContain("searchParams.get('reason')")
    expect(chooserSource).toContain('tenantRefusalReasonKey(reason)')
    expect(chooserSource, 'what is rendered is the message, not the code').toContain('{t(reasonKey)}')
  })

  it('renders no banner at all when the refusal has no explanation of its own', () => {
    expect(chooserSource, 'the banner is guarded on a key having been resolved').toContain('{reasonKey && (')
    expect(
      tenantRefusalReasonKey('permissionDenied'),
      'a caller who merely lacks a permission was not moved for a tenant reason'
    ).toBeUndefined()
  })

  it('is carried the code the middleware actually recorded, rather than a reason of the guard\'s own', () => {
    expect(guardSource).toContain('authState.tenantError')
    expect(guardSource).toContain('`/select-tenant?reason=${encodeURIComponent(authState.tenantError)}`')
    expect(
      guardSource,
      'a caller with no tenant left to choose is sent to the no-tenant screen instead, which explains itself'
    ).toContain('authState.tenants.length > 0')
  })
})
