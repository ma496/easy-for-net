import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { i18nConfig } from './config'
import { translate } from './translate'
import { emptyLocalizationResources } from './types'
import offlineResources from './offline-resources.json'

// The offline fallback is a copy of a few backend strings, bundled because the outage screen has to
// render while the API that serves them is down. A copy drifts the first time the original is
// edited, so every value is pinned to the backend's shipped resource file for the same culture.
const resourcesDirectory = fileURLToPath(new URL('../../../backend/Source/Features/Localization/Core/Resources', import.meta.url))

const offline = offlineResources as Record<string, Record<string, string>>

/** Reads one dotted key out of a nested backend resource file. */
const shippedValue = (culture: string, key: string): unknown => {
  const resources = JSON.parse(readFileSync(`${resourcesDirectory}/${culture}.json`, 'utf8'))
  return key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], resources)
}

describe('offline-resources.json', () => {
  it('covers exactly the routable locales, each with the same keys', () => {
    expect(Object.keys(offline).sort()).toEqual([...i18nConfig.locales].sort())

    const englishKeys = Object.keys(offline.en).sort()
    for (const culture of i18nConfig.locales) {
      expect(Object.keys(offline[culture]).sort(), culture).toEqual(englishKeys)
    }
  })

  it.each([...i18nConfig.locales])('matches the backend shipped values for %s', (culture) => {
    for (const [key, value] of Object.entries(offline[culture])) {
      expect(value, `${culture}: ${key}`).toBe(shippedValue(culture, key))
    }
  })
})

describe('emptyLocalizationResources', () => {
  it('translates the outage screen in the requested culture', () => {
    const { resources } = emptyLocalizationResources('en')

    expect(translate(resources, 'error.serviceUnavailable.title')).toBe(offline.en['error.serviceUnavailable.title'])
    expect(translate(resources, 'brand.name')).toBe(offline.en['brand.name'])
  })

  it('falls back to English for a culture with no bundled strings', () => {
    expect(emptyLocalizationResources('xx').resources).toEqual(offline.en)
  })

  it('still renders any other key as itself', () => {
    expect(translate(emptyLocalizationResources('en').resources, 'common.save')).toBe('common.save')
  })

  it('hands out a copy, so a caller cannot change the bundled strings', () => {
    emptyLocalizationResources('en').resources['brand.name'] = 'changed'

    expect(emptyLocalizationResources('en').resources['brand.name']).toBe(offline.en['brand.name'])
  })
})
