import { describe, expect, it } from 'vitest'
import { emptySeoPageFormValues, isPublicPath, isValidJson, isValidPriority, normalizePath, seoPageFormValues, toSeoPageFields } from './seo-page-form-values'

describe('normalizePath', () => {
  it.each([
    ['/', '/'],
    ['about', '/about'],
    ['/about/', '/about'],
    [' /about?x=1#top ', '/about'],
  ])('reads %j as %j', (value, expected) => {
    expect(normalizePath(value)).toBe(expected)
  })
})

describe('isPublicPath', () => {
  it.each(['/', '/about', '/blog/first-post', '/administrators'])('accepts %s', (path) => {
    expect(isPublicPath(path)).toBe(true)
  })

  it.each(['/admin', '/admin/seo', '/account/profile', '/api/x', '//evil'])('refuses %s', (path) => {
    expect(isPublicPath(path)).toBe(false)
  })
})

describe('isValidJson', () => {
  it('accepts an empty field and a JSON document', () => {
    expect(isValidJson('')).toBe(true)
    expect(isValidJson('{"@context":"https://schema.org","@type":"Organization"}')).toBe(true)
  })

  it('refuses text that is not JSON', () => {
    expect(isValidJson('{"@type": Organization}')).toBe(false)
  })
})

describe('isValidPriority', () => {
  it.each(['', '0', '0.5', '1', '.8'])('accepts %j', (value) => {
    expect(isValidPriority(value)).toBe(true)
  })

  it.each(['1.1', '-0.1', 'high', '1e-1'])('refuses %j', (value) => {
    expect(isValidPriority(value)).toBe(false)
  })
})

describe('toSeoPageFields', () => {
  it('sends blanks as null, every language as a null culture, and the priority as a number', () => {
    const fields = toSeoPageFields({ ...emptySeoPageFormValues, path: 'about/', title: '  About us  ', priority: '0.8', changeFrequency: 'monthly' })

    expect(fields).toMatchObject({ path: '/about', culture: null, title: 'About us', description: null, ogImageFileId: null, priority: 0.8, changeFrequency: 'monthly', jsonLd: null })
  })

  it('round-trips a stored record through the form', () => {
    const stored = toSeoPageFields({ ...emptySeoPageFormValues, path: '/contact', culture: 'fr', title: 'Contact', noIndex: true, priority: '0.3' })

    expect(toSeoPageFields(seoPageFormValues(stored))).toEqual(stored)
  })
})
