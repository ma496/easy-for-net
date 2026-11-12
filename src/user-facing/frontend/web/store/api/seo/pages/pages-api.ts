import { appApi } from '@/store/api/_app-api'
import {
  SeoPageCreateRequest,
  SeoPageCreateResponse,
  SeoPageDeleteRequest,
  SeoPageGetRequest,
  SeoPageGetResponse,
  SeoPageListRequest,
  SeoPageListResponse,
  SeoPageUpdateRequest,
  SeoPageUpdateResponse,
} from './pages-dtos'

/**
 * RTK Query API for the SEO screen: the per-page SEO records (title, description, sharing card,
 * indexing, sitemap and structured data), listed, read, created, updated and deleted. Uses the
 * 'SeoPages' tag type, one entry per record id.
 */
export const seoPagesApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['SeoPages'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      seoPageCreate: builder.mutation<SeoPageCreateResponse, SeoPageCreateRequest>({
        query: (input) => ({
          url: '/seo/pages',
          method: 'POST',
          body: input,
        }),
        invalidatesTags: ['SeoPages'],
      }),
      seoPageUpdate: builder.mutation<SeoPageUpdateResponse, SeoPageUpdateRequest>({
        query: (input) => ({
          url: `/seo/pages/${input.id}`,
          method: 'PUT',
          body: { ...input, id: undefined },
        }),
        invalidatesTags: (result, error, arg) => ['SeoPages', { type: 'SeoPages', id: arg.id }],
      }),
      seoPageDelete: builder.mutation<void, SeoPageDeleteRequest>({
        query: (input) => ({
          url: `/seo/pages/${input.id}`,
          method: 'DELETE',
        }),
        invalidatesTags: (result, error, arg) => ['SeoPages', { type: 'SeoPages', id: arg.id }],
      }),
      seoPageGet: builder.query<SeoPageGetResponse, SeoPageGetRequest>({
        query: (input) => ({
          url: `/seo/pages/${input.id}`,
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [{ type: 'SeoPages', id: arg.id }],
      }),
      seoPageList: builder.query<SeoPageListResponse, SeoPageListRequest>({
        query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds }) => ({
          url: '/seo/pages',
          params: { page, pageSize, sortField, sortDirection, search, all, includeIds },
          method: 'GET',
        }),
        providesTags: (result) => ['SeoPages', ...(result?.items?.map((item) => ({ type: 'SeoPages' as const, id: item.id })) ?? [])],
      }),
    }),
  })

export const { useSeoPageCreateMutation, useSeoPageUpdateMutation, useSeoPageDeleteMutation, useSeoPageGetQuery, useSeoPageListQuery, useLazySeoPageListQuery } = seoPagesApi
