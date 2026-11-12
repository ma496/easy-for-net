import { appApi } from '@/store/api/_app-api'
import { SettingDeleteRequest, SettingDto, SettingListResponse, SettingUpdateRequest } from './settings-dtos'

/**
 * RTK Query API for the settings admin screen: every declared setting as the site resolves it, and
 * replacing or removing the site's own overrides of one. Uses the 'Settings' tag type, one row per setting name.
 */
export const settingsApi = appApi
  .enhanceEndpoints({ addTagTypes: ['Settings'] })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      settingList: builder.query<SettingListResponse, void>({
        query: () => ({ url: '/settings', method: 'GET' }),
        providesTags: (result) => ['Settings', ...(result?.items?.map((item) => ({ type: 'Settings' as const, id: item.name })) ?? [])],
      }),
      settingUpdate: builder.mutation<SettingDto, SettingUpdateRequest>({
        query: ({ name, values }) => ({ url: `/settings/${encodeURIComponent(name)}`, method: 'PUT', body: { values } }),
        invalidatesTags: (result, error, arg) => ['Settings', { type: 'Settings', id: arg.name }],
      }),
      settingDelete: builder.mutation<void, SettingDeleteRequest>({
        query: ({ name }) => ({ url: `/settings/${encodeURIComponent(name)}`, method: 'DELETE' }),
        invalidatesTags: (result, error, arg) => ['Settings', { type: 'Settings', id: arg.name }],
      }),
    }),
  })

export const { useSettingListQuery, useSettingUpdateMutation, useSettingDeleteMutation } = settingsApi
