import offlineResources from './offline-resources.json'

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
 * What every translator falls back to when the localization API cannot be reached: the few strings
 * the outage path itself shows (the brand name and the service-unavailable screen), bundled in
 * `offline-resources.json` because the API that would serve them is the thing that is down. Every
 * other key renders as itself rather than crashing. `i18n/offline-resources.test.ts` keeps the
 * bundled copy equal to the backend's shipped values.
 */
export const emptyLocalizationResources = (culture: string): LocalizationResourcesResponse => ({
  culture,
  defaultCulture: null,
  languages: [],
  resources: { ...((offlineResources as Record<string, Record<string, string>>)[culture] ?? offlineResources.en) },
})
