import { BaseDto, ListDto, ListRequestDto, RequestBase } from '@/store/api'
import type { ChangeFrequency, TwitterCard } from '@/lib/seo/seo-dtos'

export type { ChangeFrequency, TwitterCard } from '@/lib/seo/seo-dtos'

/** The fields of one page's SEO record, as `POST /seo/pages` and `PUT /seo/pages/{id}` take them. */
export interface SeoPageFields {
  /** The locale-free path the record describes, such as `/about`. */
  path: string
  /** The culture it applies to, or `null` for every language. */
  culture: string | null
  title: string | null
  description: string | null
  keywords: string | null
  canonicalUrl: string | null
  ogTitle: string | null
  ogDescription: string | null
  /** A stored file's id, from the file upload endpoint. */
  ogImageFileId: string | null
  twitterCard: TwitterCard
  noIndex: boolean
  noFollow: boolean
  includeInSitemap: boolean
  changeFrequency: ChangeFrequency | null
  /** Between 0 and 1. */
  priority: number | null
  /** A JSON-LD document, as text. */
  jsonLd: string | null
}

/** Request body of `POST /seo/pages`. */
export interface SeoPageCreateRequest extends SeoPageFields, RequestBase {}

/** Response of `POST /seo/pages`. */
export interface SeoPageCreateResponse extends BaseDto<string>, SeoPageFields {}

/** Request of `PUT /seo/pages/{id}`. */
export interface SeoPageUpdateRequest extends BaseDto<string>, SeoPageFields, RequestBase {}

/** Response of `PUT /seo/pages/{id}`. */
export interface SeoPageUpdateResponse extends BaseDto<string>, SeoPageFields {}

/** Request of `GET /seo/pages/{id}`. */
export interface SeoPageGetRequest extends BaseDto<string>, RequestBase {}

/** Response of `GET /seo/pages/{id}`. */
export interface SeoPageGetResponse extends BaseDto<string>, SeoPageFields {
  updatedAt?: string | null
}

/** Request of `DELETE /seo/pages/{id}`. */
export interface SeoPageDeleteRequest extends BaseDto<string>, RequestBase {}

/** Request parameters of `GET /seo/pages`: paging, sorting on path, culture, title or updatedAt, and search. */
export interface SeoPageListRequest extends ListRequestDto<string>, RequestBase {}

/** One row of `GET /seo/pages`. */
export interface SeoPageListDto extends BaseDto<string> {
  path: string
  culture: string | null
  title: string | null
  noIndex: boolean
  includeInSitemap: boolean
  updatedAt: string | null
}

/** Paged response of `GET /seo/pages`. */
export interface SeoPageListResponse extends ListDto<SeoPageListDto> {}
