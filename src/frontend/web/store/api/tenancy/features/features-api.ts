import { appApi } from '@/store/api/_app-api'
import {
  FeatureValueGetRequest,
  FeatureValueGetResponse,
  FeatureValueUpdateRequest,
  FeatureValueUpdateResponse,
  MyFeaturesResponse,
} from './features-dtos'

/**
 * RTK Query API for feature management — reading and setting what a tenant or an edition is entitled
 * to, and the caller's own read of what its plan includes.
 *
 * `myFeatures` carries the 'MyFeatures' tag rather than none, because changing a tenant's
 * entitlements changes the answer, and the editor invalidates it on save. Note what it does not fix:
 * a permission gated on a feature is decided when the session is minted, so the caller keeps whatever
 * their token already carries until it is renewed.
 */
export const featuresApi = appApi
  .enhanceEndpoints({
    addTagTypes: ['FeatureValues', 'MyFeatures'],
  })
  .injectEndpoints({
    overrideExisting: false,
    endpoints: (builder) => ({
      featureValueGet: builder.query<FeatureValueGetResponse, FeatureValueGetRequest>({
        query: ({ providerName, providerKey }) => ({
          url: '/features',
          params: { providerName, providerKey },
          method: 'GET',
        }),
        providesTags: (result, error, arg) => [
          { type: 'FeatureValues', id: `${arg.providerName}:${arg.providerKey}` },
        ],
      }),
      featureValueUpdate: builder.mutation<FeatureValueUpdateResponse, FeatureValueUpdateRequest>({
        query: (input) => ({
          url: '/features',
          method: 'PUT',
          body: input,
        }),
        invalidatesTags: (result, error, arg) => [
          { type: 'FeatureValues', id: `${arg.providerName}:${arg.providerKey}` },
          'MyFeatures',
        ],
      }),
      myFeatures: builder.query<MyFeaturesResponse, void>({
        query: () => ({
          url: '/features/mine',
          method: 'GET',
        }),
        providesTags: ['MyFeatures'],
      }),
    }),
  })

export const {
  useFeatureValueGetQuery,
  useLazyFeatureValueGetQuery,
  useFeatureValueUpdateMutation,
  useMyFeaturesQuery,
  useLazyMyFeaturesQuery,
} = featuresApi
