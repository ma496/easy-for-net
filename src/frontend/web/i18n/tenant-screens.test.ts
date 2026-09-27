import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const webDirectory = fileURLToPath(new URL('..', import.meta.url))
const resourcesDirectory = fileURLToPath(new URL('../../../backend/Source/Features/Localization/Core/Resources', import.meta.url))

/** Every leaf of a shipped resource file as a dotted key path, which is the string `t` looks a message up by. */
const flatten = (value: Record<string, unknown>, prefix = ''): [string, string][] =>
  Object.entries(value).flatMap(([key, entry]) => {
    const path = prefix ? `${prefix}.${key}` : key

    return entry !== null && typeof entry === 'object' ? flatten(entry as Record<string, unknown>, path) : [[path, String(entry)]]
  })

/** Every culture the backend ships resources for, by code, as the messages a screen looks up in it. */
const messages = new Map(
  readdirSync(resourcesDirectory)
    .filter((name) => name.endsWith('.json'))
    .map((name) => [name.replace(/\.json$/, ''), new Map(flatten(JSON.parse(readFileSync(join(resourcesDirectory, name), 'utf8'))))]),
)

/** The error codes a tenant refusal can arrive with, each of which has a message of its own. */
const tenantErrorCodes = [
  'authenticationRequired',
  'permissionDenied',
  'tenantNotFound',
  'tenantSuspended',
  'notTenantMember',
  'noActiveTenant',
  'tenantRequired',
  'tenantIdentifierAlreadyExists',
  'duplicateTenantMembership',
  'lastTenantAdministrator',
  'systemCreatedTenantCannotBeModified',
  'crossTenantFileAccess',
  'platformPermissionNotGrantable',
  'tenantPermissionNotGrantable',
  'concurrentModification',
  'userSharedAcrossTenants',
]

/** The keys the tenant screens render from, one per string a person reads on them. */
const tenantScreenKeys = [
  'navigation.tenants',
  'navigation.tenantsList',
  'navigation.tenantsCreate',
  'navigation.tenantsUpdate',
  'navigation.tenantsMembers',
  'navigation.selectTenant',
  'search.tenants',
  'search.tenantsCreate',
  'table.filter.tenantStatus',
  'table.filter.allStatuses',
  'table.filter.suspended',
  'form.label.tenantName',
  'form.label.tenantIdentifier',
  'form.placeholder.tenantName',
  'form.placeholder.tenantIdentifier',
  'validation.tenantIdentifier',
  'page.tenants.title',
  'page.tenants.list.title',
  'page.tenants.create.title',
  'page.tenants.update.title',
  'page.tenants.members.title',
  'page.tenants.members.addTitle',
  'page.tenants.members.rolesTitle',
  'page.tenants.members.removeTitle',
  'page.tenants.status.active',
  'page.tenants.status.suspended',
  'page.tenants.switcher.label',
  'page.tenants.switcher.switchTo',
  'page.tenants.switcher.switchSuccess',
  'page.tenants.switcher.noTenant',
  'page.selectTenant.title',
  'page.selectTenant.description',
  'page.selectTenant.emptyDescription',
  'page.selectTenant.emptyAction',
  'page.auth.signup.tenantSectionTitle',
]

describe('tenant screen copy', () => {
  it('renders every string of the tenant screens from a key that every shipped culture defines', () => {
    for (const [locale, translated] of messages) {
      const undefinedKeys = tenantScreenKeys.filter((key) => !translated.has(key))

      expect(undefinedKeys, `${locale} cannot render these keys`).toEqual([])
    }
  })

  it('explains every tenant error code in every shipped culture', () => {
    for (const [locale, translated] of messages) {
      const unexplained = tenantErrorCodes.filter((code) => {
        const message = translated.get(`error.server.${code}`)

        return !message || message.trim().length === 0 || message === code
      })

      expect(unexplained, `${locale} would show these codes to the user`).toEqual([])
    }
  })

  it('keeps the tenant name in the message that names the tenant switched to', () => {
    for (const [locale, translated] of messages) {
      expect(translated.get('page.tenants.switcher.switchSuccess'), `${locale} dropped the placeholder`).toContain('${tenant}')
    }
  })
})

/**
 * The tenant screens - and the localization admin screen, which is held to the same rule - run in a
 * locale the user may read right to left, and there is no browser here to render them in, so what is
 * checked is the source: every spacing, alignment, inset and corner these screens decide has to be
 * decided in logical terms, so the direction of the locale flips all of them at once rather than
 * leaving half the screen pointing the wrong way.
 */

/** The directory trees holding the screens and pieces held to the logical-utility rule. */
const rtlScreenDirectories = ['app/[lang]/admin/(tenancy)', 'app/[lang]/(auth)/select-tenant', 'app/[lang]/admin/localization']

/** Shared pieces outside those trees that are part of the tenant experience. */
const rtlScreenFiles = ['components/custom/tenant-switcher.tsx']

const sourceFilesUnder = (relativeDirectory: string): string[] =>
  readdirSync(join(webDirectory, relativeDirectory), { recursive: true, encoding: 'utf8' })
    .filter((entry) => entry.endsWith('.tsx') || entry.endsWith('.ts'))
    .map((entry) => join(relativeDirectory, entry).replace(/\\/g, '/'))

const rtlScreenSources = [...rtlScreenDirectories.flatMap(sourceFilesUnder), ...rtlScreenFiles]

/**
 * A physical utility and the logical one that says the same thing without naming a side, so the failure
 * message can point at the replacement rather than just at the mistake.
 */
const physicalUtilities: [label: string, pattern: RegExp, instead: string][] = [
  ['a margin or padding on a named side', /\b([mp])([lr])-/, 'ms-/me-/ps-/pe-'],
  ['a text alignment against a named side', /\btext-(left|right)\b/, 'text-start/text-end'],
  ['an inset against a named side', /\b(?:left|right)-[0-9[]/, 'start-/end-'],
  ['a border on a named side', /\bborder-[lr]\b|\bborder-[lr]-/, 'border-s-/border-e-'],
  ['a corner on a named side', /\brounded-[lr]\b|\brounded-[lr]-/, 'rounded-s-/rounded-e-'],
  ['a float against a named side', /\bfloat-(left|right)\b/, 'float-start/float-end'],
  ['an offset along the reading axis', /-?\btranslate-x-/, 'a logical or symmetric offset'],
]

describe('screens in right-to-left locales', () => {
  it('reads the screens it is meant to be checking, so a moved screen cannot pass this by being absent', () => {
    expect(rtlScreenSources.length).toBeGreaterThanOrEqual(10)
  })

  it.each(physicalUtilities)('does not use %s, which would not follow the locale', (_label, pattern, instead) => {
    const offenders = rtlScreenSources.flatMap((file) =>
      readFileSync(join(webDirectory, file), 'utf8')
        .split('\n')
        .flatMap((line, index) => (pattern.test(line) ? [`${file}:${index + 1} uses \`${line.match(pattern)![0]}\`, which is fixed to one side - use ${instead}`] : [])),
    )

    expect(offenders).toEqual([])
  })
})
