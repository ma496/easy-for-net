import { ListDto, RequestBase } from '@/store/api'
import type { LanguageDto } from '@/i18n'

/** Request parameters for listing one culture's texts, page by page, in the acting scope. */
export interface TextListRequest extends RequestBase {
  culture: string
  filter?: string
  onlyOverridden?: boolean
  page: number
  pageSize: number
}

/** One key of one culture, with the value the shipped file declares, what applies without the acting scope's own override, and that override itself. */
export interface TextListItemDto {
  key: string
  defaultValue: string
  inheritedValue: string
  value: string | null
}

/** Paged response of one culture's texts, in the acting scope. */
export interface TextListResponse extends ListDto<TextListItemDto> {}

/** Request body for setting the acting scope's override of one key in one culture. */
export interface TextUpdateRequest extends RequestBase {
  culture: string
  key: string
  value: string
}

/** Request parameters for removing the acting scope's override of one key in one culture. */
export interface TextDeleteRequest extends RequestBase {
  culture: string
  key: string
}

/** The acting scope's effective language settings, whether they are its own or inherited, and what they would inherit if they were reset. */
export interface LanguagesGetResponse {
  languages: LanguageDto[]
  enabledCultures: string[]
  defaultCulture: string | null
  isInherited: boolean
  inheritedEnabledCultures: string[]
  inheritedDefaultCulture: string | null
}

/** Request body for setting the acting scope's own enabled languages and default. */
export interface LanguagesUpdateRequest extends RequestBase {
  enabledCultures: string[]
  defaultCulture?: string | null
}
