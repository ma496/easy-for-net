import { appApi } from '@/store/api/_app-api'
import { accountApi } from '@/store/api/identity/account/account-api'
import { setUserInfo } from '@/store/slices/authSlice'
import { AuthState } from '@/lib/utils'
import {
  TenantCreateRequest,
  TenantCreateResponse,
  TenantDeleteRequest,
  TenantDeleteResponse,
  TenantExitResponse,
  TenantGetRequest,
  TenantGetResponse,
  TenantListRequest,
  TenantListResponse,
  TenantMemberAddRequest,
  TenantMemberAddResponse,
  TenantMemberListRequest,
  TenantMemberListResponse,
  TenantMemberRemoveRequest,
  TenantMemberRemoveResponse,
  TenantMemberUpdateRolesRequest,
  TenantMemberUpdateRolesResponse,
  TenantReactivateRequest,
  TenantReactivateResponse,
  TenantSuspendRequest,
  TenantSuspendResponse,
  TenantSwitchRequest,
  TenantSwitchResponse,
  TenantUpdateRequest,
  TenantUpdateResponse
} from './tenants-dtos'

/**
 * Re-reads the account info once a membership mutation has succeeded for the signed-in account itself,
 * so the tenants the header offers - which come from that info, not from a cached query - pick up a
 * tenant the account was just added to or drop one it was just removed from. A mutation that touched
 * somebody else leaves the session state alone. A failed read keeps the previous info standing.
 */
const refreshOwnTenants = async (
  userId: string,
  { dispatch, getState, queryFulfilled }: {
    dispatch: (action: unknown) => unknown
    getState: () => unknown
    queryFulfilled: Promise<unknown>
  }
): Promise<void> => {
  try {
    await queryFulfilled
  } catch {
    return
  }

  if ((getState() as { auth: AuthState }).auth.user?.id !== userId) {
    return
  }

  const userInfo = (await dispatch(
    accountApi.endpoints.getUserInfo.initiate(undefined, { subscribe: false, forceRefetch: true })
  )) as { data?: Parameters<typeof setUserInfo>[0] }
  if (userInfo.data) {
    dispatch(setUserInfo(userInfo.data))
  }
}

/**
 * RTK Query API for tenancy: the tenant lifecycle (list, get, create, update, suspend,
 * reactivate, delete), tenant membership (list, add, replace roles, remove), self-service
 * onboarding and tenant switching. Uses the 'Tenants' and 'TenantMembers' tag types, and
 * re-declares 'Users' so member mutations can refresh the tenant-scoped user surfaces. Adding or
 * removing a member also invalidates that tenant's 'Tenants' tags, because the list and detail screens
 * show how many accounts belong to it, and re-reads the account info when the member is the caller.
 * `tenantSwitch` and `tenantExit` deliberately carry no tags: the caller drops the whole cache with
 * `appApi.util.resetApiState()` instead, so nothing from the previous tenant is refetched.
 */
export const tenantsApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['Tenants', 'TenantMembers', 'Users'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      tenantList: builder.query<TenantListResponse, TenantListRequest>({
        query: ({ page, pageSize, sortField, sortDirection, search, all, includeIds, status }) => ({
          url: '/tenants',
          params: {
            page,
            pageSize,
            sortField,
            sortDirection,
            search,
            all,
            includeIds,
            status,
          },
          method: 'GET',
        }),
        providesTags: (result) => ['Tenants', ...(result?.items?.map((item) => ({ type: 'Tenants' as const, id: item.id })) ?? [])],
      }),
      tenantGet: builder.query<TenantGetResponse, TenantGetRequest>({
        query: (input) => ({
          url: `/tenants/${input.id}`,
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [{ type: 'Tenants', id: arg.id }],
      }),
      tenantCreate: builder.mutation<TenantCreateResponse, TenantCreateRequest>({
        query: (input) => ({
          url: '/tenants',
          method: 'POST',
          body: input,
        }),
        invalidatesTags: ['Tenants'],
      }),
      tenantUpdate: builder.mutation<TenantUpdateResponse, TenantUpdateRequest>({
        query: (input) => ({
          url: `/tenants/${input.id}`,
          method: 'PUT',
          body: { ...input, id: undefined },
        }),
        invalidatesTags: (result, error, arg) => ['Tenants', { type: 'Tenants', id: arg.id }],
      }),
      tenantSuspend: builder.mutation<TenantSuspendResponse, TenantSuspendRequest>({
        query: (input) => ({
          url: `/tenants/${input.id}/suspend`,
          method: 'POST',
        }),
        invalidatesTags: (result, error, arg) => ['Tenants', { type: 'Tenants', id: arg.id }],
      }),
      tenantReactivate: builder.mutation<TenantReactivateResponse, TenantReactivateRequest>({
        query: (input) => ({
          url: `/tenants/${input.id}/reactivate`,
          method: 'POST',
        }),
        invalidatesTags: (result, error, arg) => ['Tenants', { type: 'Tenants', id: arg.id }],
      }),
      tenantDelete: builder.mutation<TenantDeleteResponse, TenantDeleteRequest>({
        query: (input) => ({
          url: `/tenants/${input.id}`,
          method: 'DELETE',
        }),
        invalidatesTags: (result, error, arg) => ['Tenants', { type: 'Tenants', id: arg.id }],
      }),
      tenantMemberList: builder.query<TenantMemberListResponse, TenantMemberListRequest>({
        query: ({ tenantId, page, pageSize, sortField, sortDirection, search, all, includeIds, roleId }) => ({
          url: `/tenants/${tenantId}/members`,
          params: {
            page,
            pageSize,
            sortField,
            sortDirection,
            search,
            all,
            includeIds,
            roleId,
          },
          method: 'GET',
        }),
        // A row's id is the member's user account id, which is how every member mutation
        // addresses the member, so the row tag and the invalidation line up.
        providesTags: (result) => ['TenantMembers', ...(result?.items?.map((item) => ({ type: 'TenantMembers' as const, id: item.id })) ?? [])],
      }),
      tenantMemberAdd: builder.mutation<TenantMemberAddResponse, TenantMemberAddRequest>({
        query: (input) => ({
          url: `/tenants/${input.tenantId}/members`,
          method: 'POST',
          body: { ...input, tenantId: undefined },
        }),
        invalidatesTags: (result, error, arg) => ['TenantMembers', 'Users', 'Tenants', { type: 'Tenants', id: arg.tenantId }],
        onQueryStarted: (arg, api) => refreshOwnTenants(arg.userId, api),
      }),
      tenantMemberUpdateRoles: builder.mutation<TenantMemberUpdateRolesResponse, TenantMemberUpdateRolesRequest>({
        query: (input) => ({
          url: `/tenants/${input.tenantId}/members/${input.userId}/roles`,
          method: 'PUT',
          body: { ...input, tenantId: undefined, userId: undefined },
        }),
        invalidatesTags: (result, error, arg) => [
          'TenantMembers',
          { type: 'TenantMembers', id: arg.userId },
          'Users',
          { type: 'Users', id: arg.userId },
        ],
      }),
      tenantMemberRemove: builder.mutation<TenantMemberRemoveResponse, TenantMemberRemoveRequest>({
        query: (input) => ({
          url: `/tenants/${input.tenantId}/members/${input.userId}`,
          method: 'DELETE',
        }),
        invalidatesTags: (result, error, arg) => [
          'TenantMembers',
          { type: 'TenantMembers', id: arg.userId },
          'Users',
          { type: 'Users', id: arg.userId },
          'Tenants',
          { type: 'Tenants', id: arg.tenantId },
        ],
        onQueryStarted: (arg, api) => refreshOwnTenants(arg.userId, api),
      }),
      // No tags: switching tenants must drop the whole cache rather than refresh parts of it,
      // so the caller dispatches appApi.util.resetApiState() on success. Invalidating here
      // would refetch the previous tenant's queries during the switch.
      tenantSwitch: builder.mutation<TenantSwitchResponse, TenantSwitchRequest>({
        query: (input) => ({
          url: '/tenants/switch',
          method: 'POST',
          body: input,
        }),
      }),
      // Untagged for the same reason as tenantSwitch: leaving a tenant changes what every cached
      // query would answer, so the caller drops the whole cache rather than refreshing parts of it.
      tenantExit: builder.mutation<TenantExitResponse, void>({
        query: () => ({
          url: '/tenants/exit',
          method: 'POST',
        }),
      }),
    }),
  })

export const {
  useTenantListQuery,
  useLazyTenantListQuery,
  useTenantGetQuery,
  useLazyTenantGetQuery,
  useTenantCreateMutation,
  useTenantUpdateMutation,
  useTenantSuspendMutation,
  useTenantReactivateMutation,
  useTenantDeleteMutation,
  useTenantMemberListQuery,
  useLazyTenantMemberListQuery,
  useTenantMemberAddMutation,
  useTenantMemberUpdateRolesMutation,
  useTenantMemberRemoveMutation,
  useTenantSwitchMutation,
  useTenantExitMutation
} = tenantsApi
