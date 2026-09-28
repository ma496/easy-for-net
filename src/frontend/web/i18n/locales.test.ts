import { readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { i18nConfig } from './config'

// Translations are served by the API from the backend's shipped resource files; the web app does not
// bundle them. Their *content* (key parity, empty values, …) is a backend concern, checked by
// `Tests/Features/Localization`. What the web app owns is its own routing: every culture the API can
// serve must be one this project routes to, and vice versa, or a visitor could land on a locale
// segment the proxy never rewrites for.
const resourcesDirectory = fileURLToPath(new URL('../../../backend/Source/Features/Localization/Core/Resources', import.meta.url))

const shippedLocales = readdirSync(resourcesDirectory)
  .filter((name) => name.endsWith('.json'))
  .map((name) => name.replace(/\.json$/, ''))

describe('i18nConfig.locales', () => {
  it('routes exactly the cultures the backend ships resources for, and no others', () => {
    expect([...i18nConfig.locales].sort()).toEqual([...shippedLocales].sort())
  })
})
