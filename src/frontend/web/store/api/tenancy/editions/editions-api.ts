import { appApi } from '@/store/api/_app-api'
import {
  EditionCreateRequest,
  EditionCreateResponse,
  EditionDeleteRequest,
  EditionDeleteResponse,
  EditionGetRequest,
  EditionGetResponse,
  EditionListRequest,
  EditionListResponse,
  EditionUpdateRequest,
  EditionUpdateResponse,
} from './editions-dtos'

/**
 * RTK Query API for editions — the plans the platform sells. Uses the 'Editions' tag type, and
 * re-declares 'Tenants' because a plan's tenant count is read off the tenant rows: deleting a plan,
 * or moving a tenant onto one, changes what the other surface shows.
 */
export const editionsApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['Editions', 'Tenants'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      editionList: builder.query<EditionListResponse, EditionListRequest>({
        query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds }) => ({
          url: '/editions',
          params: {
            page,
            pageSize,
            sortField,
            sortDirection,
            search,
            all,
            includeIds,
          },
          method: 'GET',
        }),
        providesTags: (result) => [
          'Editions',
          ...(result?.items?.map((item) => ({ type: 'Editions' as const, id: item.id })) ?? []),
        ],
      }),
      editionGet: builder.query<EditionGetResponse, EditionGetRequest>({
        query: (input) => ({
          url: `/editions/${input.id}`,
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [{ type: 'Editions', id: arg.id }],
      }),
      editionCreate: builder.mutation<EditionCreateResponse, EditionCreateRequest>({
        query: (input) => ({
          url: '/editions',
          method: 'POST',
          body: input,
        }),
        invalidatesTags: ['Editions'],
      }),
      editionUpdate: builder.mutation<EditionUpdateResponse, EditionUpdateRequest>({
        query: (input) => ({
          url: `/editions/${input.id}`,
          method: 'PUT',
          body: { ...input, id: undefined },
        }),
        invalidatesTags: (result, error, arg) => ['Editions', { type: 'Editions', id: arg.id }],
      }),
      editionDelete: builder.mutation<EditionDeleteResponse, EditionDeleteRequest>({
        query: (input) => ({
          url: `/editions/${input.id}`,
          method: 'DELETE',
        }),
        invalidatesTags: ['Editions', 'Tenants'],
      }),
    }),
  })

export const {
  useEditionListQuery,
  useLazyEditionListQuery,
  useEditionGetQuery,
  useLazyEditionGetQuery,
  useEditionCreateMutation,
  useEditionUpdateMutation,
  useEditionDeleteMutation,
} = editionsApi
