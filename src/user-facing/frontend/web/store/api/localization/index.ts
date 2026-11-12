export {
  localizationApi,
  useLocalizationTextListQuery,
  useLocalizationTextUpsertMutation,
  useLocalizationTextDeleteMutation,
  useLocalizationLanguagesGetQuery,
  useLocalizationLanguagesUpdateMutation,
  useLocalizationLanguagesResetMutation,
} from './localization/localization-api'
export type {
  TextListRequest,
  TextListResponse,
  TextListItemDto,
  TextUpdateRequest,
  TextDeleteRequest,
  LanguagesGetResponse,
  LanguagesUpdateRequest,
} from './localization/localization-dtos'
