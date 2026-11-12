'use client'

import { useState } from 'react'
import * as Yup from 'yup'
import { Form, Formik, FormikHelpers } from 'formik'
import { useTranslation } from '@/i18n'
import { FormCheckbox, FormInput, FormTextarea } from '@/components/ui/form'
import { FormImageUpload } from '@/components/custom'
import { successToast } from '@/lib/utils'
import { revalidatePublicCache } from '@/lib/revalidate-client'
import { SeoSettingsDto, SettingName, toSeoSettings, useSettingUpdateMutation } from '@/store/api/settings'
import { ChangedBadge, SettingField, SettingsCard, SourceBadge } from './settings-card-parts'
import { buildSeoUpdateValues, hasOwnOverride, isValidSiteUrl, isValidTitleTemplate, markerOf, seoFormValues, SeoFormValues, seoSources } from './settings-form'
import { useSettingReset } from './use-setting-reset'

/** Props for the SEO card: the resolved setting and whether it may be edited. */
interface SeoSettingsCardProps {
  settings: SeoSettingsDto
  canUpdate: boolean
  onReset: () => Promise<void>
}

/** Client-side checks for shape; the API has the last word. */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) =>
  Yup.object().shape({
    siteName: Yup.string().max(100, t('validation.maxLength', { max: 100 }) || `Field must not exceed ${100} characters`),
    titleTemplate: Yup.string().test('template', t('page.settings.seo.titleTemplateInvalid') || 'The template must contain %s, where each page title goes', (value) => isValidTitleTemplate(value)),
    defaultDescription: Yup.string().max(300, t('validation.maxLength', { max: 300 }) || `Field must not exceed ${300} characters`),
    siteUrl: Yup.string().test('url', t('page.settings.seo.siteUrlInvalid') || 'Enter the site address, such as https://www.example.com', (value) => isValidSiteUrl(value)),
    twitterSite: Yup.string().max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
  })

/** The text properties of the SEO setting, in the order the grid lays them out. */
type SeoTextField = 'siteName' | 'titleTemplate' | 'siteUrl' | 'twitterSite'

/**
 * The `Seo` setting: the site-wide defaults every public page's metadata falls back to - the site name,
 * the title pattern, the description, the sharing image and the X (Twitter) account - plus the site's
 * address, whether search engines may index it at all, and extra `robots.txt` lines. A save also clears
 * the public pages' cached metadata, so the change shows on the next request.
 */
export const SeoSettingsCard = ({ settings: initialSettings, canUpdate, onReset }: SeoSettingsCardProps) => {
  const { t } = useTranslation()
  const title = t('page.settings.seo.title') || 'SEO'
  const [settings, setSettings] = useState(initialSettings)
  const [updateSetting, { isLoading: isSaving, error: saveError, reset: clearSaveError }] = useSettingUpdateMutation()
  const { reset, isResetting } = useSettingReset(SettingName.Seo, title, async () => {
    await revalidatePublicCache(['seo'])
    await onReset()
  })
  const validationSchema = createValidationSchema(t)

  const onSubmit = async (values: SeoFormValues, { resetForm }: FormikHelpers<SeoFormValues>) => {
    const result = await updateSetting({ name: SettingName.Seo, values: buildSeoUpdateValues(settings, seoFormValues(settings), values) })
    if (result.error) return

    const next = toSeoSettings(result.data) ?? settings
    setSettings(next)
    resetForm({ values: seoFormValues(next) })
    await revalidatePublicCache(['seo'])
    successToast.fire({ text: t('page.settings.saveSuccess', { setting: title }) || `${title} settings saved` })
  }

  const idOf = (field: keyof SeoFormValues) => `setting-seo-${field}`

  return (
    <Formik<SeoFormValues> initialValues={seoFormValues(initialSettings)} validationSchema={validationSchema} onSubmit={onSubmit}>
      {({ values, initialValues, dirty, resetForm }) => {
        const textField = (field: SeoTextField, label: string, hint: string, inputProps: React.InputHTMLAttributes<HTMLInputElement> = {}) => (
          <SettingField id={idOf(field)} label={label} marker={markerOf(settings[field].source)} changed={values[field] !== initialValues[field]}>
            <FormInput id={idOf(field)} name={field} disabled={!canUpdate} hint={hint} {...inputProps} />
          </SettingField>
        )

        return (
          <Form noValidate>
            <SettingsCard
              title={title}
              description={t('page.settings.seo.description') || 'Site-wide defaults for search engines and social sharing. Each page can override them on the SEO screen.'}
              isCustomized={hasOwnOverride(seoSources(settings))}
              canUpdate={canUpdate}
              dirty={dirty}
              isSaving={isSaving}
              isResetting={isResetting}
              saveError={saveError}
              onReset={reset}
              onDiscard={() => {
                resetForm()
                clearSaveError()
              }}
            >
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {textField('siteName', t('page.settings.seo.siteName') || 'Site name', t('page.settings.seo.siteNameHelp') || 'Shown in titles and when a page is shared.')}
                {textField('titleTemplate', t('page.settings.seo.titleTemplate') || 'Title template', t('page.settings.seo.titleTemplateHelp') || 'For example "%s | My Site". %s is replaced by the page title.', { dir: 'ltr' })}
                {textField('siteUrl', t('page.settings.seo.siteUrl') || 'Site address', t('page.settings.seo.siteUrlHelp') || 'The public address canonical links and the sitemap are built from.', { dir: 'ltr', type: 'url', inputMode: 'url' })}
                {textField('twitterSite', t('page.settings.seo.twitterSite') || 'X (Twitter) account', t('page.settings.seo.twitterSiteHelp') || 'The @handle shown on shared cards.', { dir: 'ltr' })}
              </div>

              <SettingField id={idOf('defaultDescription')} label={t('page.settings.seo.defaultDescription') || 'Default description'} marker={markerOf(settings.defaultDescription.source)} changed={values.defaultDescription !== initialValues.defaultDescription}>
                <FormTextarea id={idOf('defaultDescription')} name="defaultDescription" rows={3} disabled={!canUpdate} />
              </SettingField>

              <FormImageUpload
                name="defaultOgImageFileId"
                label={t('page.settings.seo.defaultOgImage') || 'Default sharing image'}
                hint={t('page.settings.seo.defaultOgImageHelp') || 'Used when a page has no image of its own. 1200 x 630 pixels works best.'}
                disabled={!canUpdate}
                labelAddon={
                  <>
                    <SourceBadge marker={markerOf(settings.defaultOgImageFileId.source)} />
                    {values.defaultOgImageFileId !== initialValues.defaultOgImageFileId && <ChangedBadge />}
                  </>
                }
              />

              <div>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <FormCheckbox id={idOf('allowIndexing')} name="allowIndexing" label={t('page.settings.seo.allowIndexing') || 'Allow search engines to index the site'} disabled={!canUpdate} />
                  <div className="flex flex-wrap items-center gap-1">
                    <SourceBadge marker={markerOf(settings.allowIndexing.source)} />
                    {values.allowIndexing !== initialValues.allowIndexing && <ChangedBadge />}
                  </div>
                </div>
                <p className="mt-1 text-xs text-muted-foreground">{t('page.settings.seo.allowIndexingHelp') || 'When off, robots.txt asks every crawler to stay away. Turn it off on staging sites.'}</p>
              </div>

              <SettingField id={idOf('robotsExtra')} label={t('page.settings.seo.robotsExtra') || 'Extra robots.txt lines'} marker={markerOf(settings.robotsExtra.source)} changed={values.robotsExtra !== initialValues.robotsExtra}>
                <FormTextarea id={idOf('robotsExtra')} name="robotsExtra" rows={4} dir="ltr" className="font-mono" disabled={!canUpdate} />
              </SettingField>
            </SettingsCard>
          </Form>
        )
      }}
    </Formik>
  )
}
