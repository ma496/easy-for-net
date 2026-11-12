import { appApi } from '@/store/api/_app-api'
import {
  UserCreateRequest,
  UserCreateResponse,
  UserDeleteRequest,
  UserDeleteResponse,
  UserGetRequest,
  UserGetResponse,
  UserListRequest,
  UserListResponse,
  UserUpdateRequest,
  UserUpdateResponse
} from './users-dtos'

/**
 * RTK Query API for user management: CRUD on users and a paginated
 * listing endpoint that supports filtering by isActive/roleId. Uses
 * the 'Users' tag type for cache invalidation across the feature.
 */
export const usersApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['Users'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      userCreate: builder.mutation<UserCreateResponse, UserCreateRequest>({
        query: (input) => ({
          url: '/users',
          method: 'POST',
          body: input,
        }),
        invalidatesTags: ['Users'],
      }),
      userUpdate: builder.mutation<UserUpdateResponse, UserUpdateRequest>({
        query: (input) => ({
          url: `/users/${input.id}`,
          method: 'PUT',
          body: { ...input, id: undefined },
        }),
        invalidatesTags: (result, error, arg) => ['Users', { type: 'Users', id: arg.id }],
      }),
      userDelete: builder.mutation<UserDeleteResponse, UserDeleteRequest>({
        query: (input) => ({
          url: `/users/${input.id}`,
          method: 'DELETE',
        }),
        invalidatesTags: (result, error, arg) => ['Users', { type: 'Users', id: arg.id }],
      }),
      userGet: builder.query<UserGetResponse, UserGetRequest>({
        query: (input) => ({
          url: `/users/${input.id}`,
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [{ type: 'Users', id: arg.id }],
      }),
      userList: builder.query<UserListResponse, UserListRequest>({
        query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds, isActive, roleId }) => ({
          url: '/users',
          params: {
            page,
            pageSize,
            sortField,
            sortDirection,
            search,
            all,
            includeIds,
            isActive,
            roleId,
          },
          method: 'GET',
        }),
        providesTags: (result) => ['Users', ...(result?.items?.map((item) => ({ type: 'Users' as const, id: item.id })) ?? [])],
      }),
    }),
  })

export const { useUserCreateMutation, useUserUpdateMutation, useUserDeleteMutation, useUserGetQuery, useLazyUserGetQuery, useUserListQuery, useLazyUserListQuery } = usersApi
