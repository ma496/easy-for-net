import { describe, expect, it } from 'vitest'
import { FeatureNames } from './feature-names'

/**
 * The API's `FeatureNames` catalog, restated here as the client must name them. The pair is only
 * useful if the two sides agree: a feature renamed on one side alone silently resolves to nothing,
 * and a permission gated on it would disappear for every tenant with no compile error anywhere.
 */
const features = {
  Identity_UserManagement: 'Identity.UserManagement',
  Identity_MaxUserCount: 'Identity.MaxUserCount',
  FileManagement_Enabled: 'FileManagement.Enabled',
  FileManagement_MaxFileSizeMb: 'FileManagement.MaxFileSizeMb',
} as const

describe('FeatureNames', () => {
  it.each(Object.entries(features))('mirrors %s as %s', (name, value) => {
    expect(FeatureNames[name as keyof typeof features]).toBe(value)
  })

  it('carries every feature the API declares, and no others', () => {
    expect(Object.keys(FeatureNames).sort()).toEqual(Object.keys(features).sort())
  })

  it('gives no two features the same value', () => {
    const values = Object.values(FeatureNames)

    expect(new Set(values).size).toBe(values.length)
  })

  it('names every feature as the group and the capability it belongs to', () => {
    for (const value of Object.values(FeatureNames)) {
      expect(value).toMatch(/^[A-Z][A-Za-z]*\.[A-Z][A-Za-z]*$/)
    }
  })
})
