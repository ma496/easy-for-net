import { appApi } from '@/store/api/_app-api'
import { PermissionDefinitionResponse, PermissionResponse } from './permissions-dtos'

/**
 * RTK Query API exposing endpoints to fetch the permission catalog
 * (grouped permission definitions) and the current user's assigned
 * permissions.
 */
export const permissionsApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder) => ({
    getDefinePermissions: builder.query<PermissionDefinitionResponse, void>({
      query: () => ({
        url: '/permissions/define',
        method: 'GET',
      }),
    }),
    getPermissions: builder.query<PermissionResponse, void>({
      query: () => ({
        url: '/permissions/get',
        method: 'GET',
      }),
    }),
  }),
})

export const { useGetDefinePermissionsQuery, useLazyGetDefinePermissionsQuery, useGetPermissionsQuery, useLazyGetPermissionsQuery } = permissionsApi
