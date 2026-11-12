import type { ChangeFrequency, SeoPageFields, TwitterCard } from '@/store/api/seo'

/** The value the language select uses for "every language" (`culture: null` on the wire). */
export const ALL_CULTURES = ''

/** The value the change-frequency select uses for "not stated" (`changeFrequency: null` on the wire). */
export const NO_FREQUENCY = ''

/** The change frequencies the sitemap protocol knows, in the order the select offers them. */
export const CHANGE_FREQUENCIES: ChangeFrequency[] = ['always', 'hourly', 'daily', 'weekly', 'monthly', 'yearly', 'never']

/** The SEO page form's values: every text a string (empty meaning "not set"), the priority as text. */
export interface SeoPageFormValues {
  path: string
  culture: string
  title: string
  description: string
  keywords: string
  canonicalUrl: string
  ogTitle: string
  ogDescription: string
  ogImageFileId: string
  twitterCard: TwitterCard
  noIndex: boolean
  noFollow: boolean
  includeInSitemap: boolean
  changeFrequency: string
  priority: string
  jsonLd: string
}

/** The form a new record starts from: the home page, every language, indexed and in the sitemap. */
export const emptySeoPageFormValues: SeoPageFormValues = {
  path: '/',
  culture: ALL_CULTURES,
  title: '',
  description: '',
  keywords: '',
  canonicalUrl: '',
  ogTitle: '',
  ogDescription: '',
  ogImageFileId: '',
  twitterCard: 'summary_large_image',
  noIndex: false,
  noFollow: false,
  includeInSitemap: true,
  changeFrequency: NO_FREQUENCY,
  priority: '',
  jsonLd: '',
}

/** The form seeded from a stored record. */
export const seoPageFormValues = (fields: SeoPageFields): SeoPageFormValues => ({
  path: fields.path,
  culture: fields.culture ?? ALL_CULTURES,
  title: fields.title ?? '',
  description: fields.description ?? '',
  keywords: fields.keywords ?? '',
  canonicalUrl: fields.canonicalUrl ?? '',
  ogTitle: fields.ogTitle ?? '',
  ogDescription: fields.ogDescription ?? '',
  ogImageFileId: fields.ogImageFileId ?? '',
  twitterCard: fields.twitterCard,
  noIndex: fields.noIndex,
  noFollow: fields.noFollow,
  includeInSitemap: fields.includeInSitemap,
  changeFrequency: fields.changeFrequency ?? NO_FREQUENCY,
  priority: fields.priority === null || fields.priority === undefined ? '' : String(fields.priority),
  jsonLd: fields.jsonLd ?? '',
})

/** Trimmed text, or `null` when nothing is left - the API stores "not set" as null. */
const textOrNull = (value: string): string | null => {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

/** A path as the API keys it: leading slash, no trailing one (except the home page), no query or fragment. */
export const normalizePath = (value: string): string => {
  const path = value.trim().split(/[?#]/)[0]
  const withSlash = path.startsWith('/') ? path : `/${path}`
  return withSlash.length > 1 ? withSlash.replace(/\/+$/, '') || '/' : withSlash
}

/** Whether the text is a JSON document - an empty field counts as "none", which is valid. */
export const isValidJson = (value: string | null | undefined): boolean => {
  if (!value?.trim()) return true
  try {
    JSON.parse(value)
    return true
  } catch {
    return false
  }
}

/** Whether the priority is empty, or a number from 0 to 1. */
export const isValidPriority = (value: string | null | undefined): boolean => {
  const trimmed = (value ?? '').trim()
  if (!trimmed) return true
  if (!/^\d*\.?\d+$/.test(trimmed)) return false
  const priority = Number(trimmed)
  return priority >= 0 && priority <= 1
}

/** Whether a path names a public page: absolute, and outside the admin area, the account screens and the API. */
export const isPublicPath = (value: string | null | undefined): boolean => {
  const raw = (value ?? '').trim()
  if (raw.startsWith('//')) return false
  const path = normalizePath(raw)
  return !/^\/(admin|account|api)(\/|$)/.test(path) && !path.includes('//')
}

/** The body `POST /seo/pages` and `PUT /seo/pages/{id}` take, from the form. */
export const toSeoPageFields = (values: SeoPageFormValues): SeoPageFields => ({
  path: normalizePath(values.path),
  culture: values.culture === ALL_CULTURES ? null : values.culture,
  title: textOrNull(values.title),
  description: textOrNull(values.description),
  keywords: textOrNull(values.keywords),
  canonicalUrl: textOrNull(values.canonicalUrl),
  ogTitle: textOrNull(values.ogTitle),
  ogDescription: textOrNull(values.ogDescription),
  ogImageFileId: textOrNull(values.ogImageFileId),
  twitterCard: values.twitterCard,
  noIndex: values.noIndex,
  noFollow: values.noFollow,
  includeInSitemap: values.includeInSitemap,
  changeFrequency: values.changeFrequency === NO_FREQUENCY ? null : (values.changeFrequency as ChangeFrequency),
  priority: values.priority.trim() ? Number(values.priority.trim()) : null,
  jsonLd: textOrNull(values.jsonLd),
})
