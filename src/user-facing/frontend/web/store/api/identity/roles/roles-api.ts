import { appApi } from '@/store/api/_app-api'
import {
  RoleCreateResponse,
  RoleCreateRequest,
  RoleUpdateRequest,
  RoleUpdateResponse,
  RoleDeleteRequest,
  RoleDeleteResponse,
  RoleGetRequest,
  RoleGetResponse,
  RoleListResponse,
  RoleListRequest,
  ChangePermissionsRequest,
  ChangePermissionsResponse
} from './roles-dtos'

/**
 * RTK Query API for role management: CRUD on roles plus a dedicated
 * endpoint to change a role's permission set. Tag-invalidates the
 * 'Roles' collection on mutations to keep lists/details in sync.
 */
export const rolesApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['Roles'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      roleCreate: builder.mutation<RoleCreateResponse, RoleCreateRequest>({
        query: (input) => ({
          url: '/roles',
          method: 'POST',
          body: input,
        }),
        invalidatesTags: ['Roles'],
      }),
      roleUpdate: builder.mutation<RoleUpdateResponse, RoleUpdateRequest>({
        query: (input) => ({
          url: `/roles/${input.id}`,
          method: 'PUT',
          body: { ...input, id: undefined },
        }),
        invalidatesTags: (result, error, arg) => ['Roles', { type: 'Roles', id: arg.id }],
      }),
      roleDelete: builder.mutation<RoleDeleteResponse, RoleDeleteRequest>({
        query: (input) => ({
          url: `/roles/${input.id}`,
          method: 'DELETE',
        }),
        invalidatesTags: (result, error, arg) => ['Roles', { type: 'Roles', id: arg.id }],
      }),
      roleGet: builder.query<RoleGetResponse, RoleGetRequest>({
        query: (input) => ({
          url: `/roles/${input.id}`,
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [{ type: 'Roles', id: arg.id }],
      }),
      roleList: builder.query<RoleListResponse, RoleListRequest>({
        query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds }) => ({
          url: '/roles',
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
        providesTags: (result) => ['Roles', ...(result?.items?.map((item) => ({ type: 'Roles' as const, id: item.id })) ?? [])],
      }),
      changePermissions: builder.mutation<ChangePermissionsResponse, ChangePermissionsRequest>({
        query: (input) => ({
          url: `/roles/change-permissions/${input.id}`,
          method: 'PUT',
          body: input,
        }),
        invalidatesTags: (result, error, arg) => ['Roles', { type: 'Roles', id: arg.id }],
      }),
    }),
  })

export const { useRoleCreateMutation, useRoleUpdateMutation, useRoleDeleteMutation, useRoleGetQuery, useLazyRoleGetQuery, useRoleListQuery, useLazyRoleListQuery, useChangePermissionsMutation } =
  rolesApi
