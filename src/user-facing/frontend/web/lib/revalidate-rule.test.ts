import { describe, expect, it } from 'vitest'
import { Allow } from '@/allow'
import { decideRevalidation } from './revalidate-rule'

const holding = (...permissions: string[]) => new Set(permissions)

describe('decideRevalidation', () => {
  it('clears the SEO cache for a caller holding Seo.Update', () => {
    expect(decideRevalidation({ tags: ['seo'] }, holding(Allow.Seo_Update))).toEqual({ kind: 'revalidate', tags: ['seo'] })
  })

  it('clears the dictionary cache for a caller holding Localization.Update', () => {
    expect(decideRevalidation({ tags: ['localization', 'localization'] }, holding(Allow.Localization_Update))).toEqual({ kind: 'revalidate', tags: ['localization'] })
  })

  it('refuses a tag the caller lacks the permission for, even beside one they hold', () => {
    expect(decideRevalidation({ tags: ['seo'] }, holding(Allow.Seo_View, Allow.Localization_Update))).toEqual({ kind: 'forbidden' })
    expect(decideRevalidation({ tags: ['seo', 'localization'] }, holding(Allow.Seo_Update))).toEqual({ kind: 'forbidden' })
  })

  it.each([undefined, null, {}, { tags: [] }, { tags: 'seo' }, { tags: ['users'] }, { tags: ['seo', 42] }, { tags: ['__proto__'] }])('refuses the body %j', (body) => {
    expect(decideRevalidation(body, holding(Allow.Seo_Update, Allow.Localization_Update))).toEqual({ kind: 'badRequest' })
  })
})
