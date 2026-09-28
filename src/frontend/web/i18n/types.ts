/** A language the platform ships resources for, as the localization API reports it. */
export interface LanguageDto {
  code: string
  name: string
  isRtl: boolean
}

/**
 * The merged localization resources for one culture - the shipped strings with any platform or
 * tenant override applied - as `GET /localization/resources/{culture}` returns them. `resources`
 * is a flat, dotted-key dictionary (`"common.save"`).
 */
export interface LocalizationResourcesResponse {
  culture: string
  defaultCulture: string | null
  languages: LanguageDto[]
  resources: Record<string, string>
}

/**
 * What every translator falls back to when the localization API cannot be reached, so a page
 * renders its translation keys instead of crashing.
 */
export const emptyLocalizationResources = (culture: string): LocalizationResourcesResponse => ({
  culture,
  defaultCulture: null,
  languages: [],
  resources: {},
})
