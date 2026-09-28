import { appApi } from '@/store/api/_app-api'
import {
  LanguagesGetResponse,
  LanguagesUpdateRequest,
  TextDeleteRequest,
  TextListRequest,
  TextListResponse,
  TextUpdateRequest,
} from './localization-dtos'

/**
 * RTK Query API for the localization admin screen: the acting scope's text overrides (paged,
 * filterable by culture) and its language settings (which cultures are enabled, and the default).
 * Both mutate one scope - the caller's tenant, or the platform when acting in none - never another's.
 */
export const localizationApi = appApi
  .enhanceEndpoints({ addTagTypes: ['LocalizationText', 'LocalizationLanguage'] })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      localizationTextList: builder.query<TextListResponse, TextListRequest>({
        query: ({ culture, filter, onlyOverridden, page, pageSize }) => ({
          url: '/localization/texts',
          method: 'GET',
          params: {
            culture,
            page,
            pageSize,
            ...(filter ? { filter } : {}),
            ...(onlyOverridden ? { onlyOverridden } : {}),
          },
        }),
        providesTags: (result) => [
          'LocalizationText',
          ...(result?.items?.map((item) => ({ type: 'LocalizationText' as const, id: item.key })) ?? []),
        ],
      }),
      localizationTextUpsert: builder.mutation<void, TextUpdateRequest>({
        query: (input) => ({ url: '/localization/texts', method: 'PUT', body: input }),
        invalidatesTags: (result, error, arg) => ['LocalizationText', { type: 'LocalizationText', id: arg.key }],
      }),
      localizationTextDelete: builder.mutation<void, TextDeleteRequest>({
        query: ({ culture, key }) => ({ url: '/localization/texts', method: 'DELETE', params: { culture, key } }),
        invalidatesTags: (result, error, arg) => ['LocalizationText', { type: 'LocalizationText', id: arg.key }],
      }),
      localizationLanguagesGet: builder.query<LanguagesGetResponse, void>({
        query: () => ({ url: '/localization/languages', method: 'GET' }),
        providesTags: ['LocalizationLanguage'],
      }),
      localizationLanguagesUpdate: builder.mutation<void, LanguagesUpdateRequest>({
        query: (input) => ({ url: '/localization/languages', method: 'PUT', body: input }),
        invalidatesTags: ['LocalizationLanguage'],
      }),
      localizationLanguagesReset: builder.mutation<void, void>({
        query: () => ({ url: '/localization/languages', method: 'DELETE' }),
        invalidatesTags: ['LocalizationLanguage'],
      }),
    }),
  })

export const {
  useLocalizationTextListQuery,
  useLocalizationTextUpsertMutation,
  useLocalizationTextDeleteMutation,
  useLocalizationLanguagesGetQuery,
  useLocalizationLanguagesUpdateMutation,
  useLocalizationLanguagesResetMutation,
} = localizationApi
