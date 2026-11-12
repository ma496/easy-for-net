'use client'

import { useContext } from 'react'
import * as Yup from 'yup'
import { Form, Formik, FormikHelpers } from 'formik'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { Button } from '@/components/ui'
import { FormCheckbox, FormInput, FormSelect, FormTextarea } from '@/components/ui/form'
import { FormImageUpload } from '@/components/custom'
import { TranslationContext } from '@/components/layouts/translation-provider'
import { apiErrorAlert, getErrorCode, successToast } from '@/lib/utils'
import { adminPath } from '@/lib/paths'
import { revalidatePublicCache } from '@/lib/revalidate-client'
import type { ApiError } from '@/lib/utils'
import type { SeoPageFields } from '@/store/api/seo'
import { FormActions, FormSection } from '@/app/[lang]/admin/_components/form-layout'
import { ALL_CULTURES, CHANGE_FREQUENCIES, isPublicPath, isValidJson, isValidPriority, NO_FREQUENCY, SeoPageFormValues, toSeoPageFields } from './seo-page-form-values'

/** The id of the path input, so a duplicate-record refusal can put the caret back in it. */
const pathInputId = 'seo-page-path'

/** The ids of the title and description character counters, which their inputs name as descriptions. */
const titleCounterId = 'seo-page-title-count'
const descriptionCounterId = 'seo-page-description-count'

/** Builds the form's Yup schema with translated messages. */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) =>
  Yup.object().shape({
    path: Yup.string()
      .trim()
      .required(t('validation.required') || 'Field is required')
      .max(400, t('validation.maxLength', { max: 400 }) || `Field must not exceed ${400} characters`)
      .test('public', t('page.seo.form.pathInvalid') || 'Enter the path of a public page, such as /about', (value) => isPublicPath(value)),
    title: Yup.string().max(120, t('validation.maxLength', { max: 120 }) || `Field must not exceed ${120} characters`),
    description: Yup.string().max(320, t('validation.maxLength', { max: 320 }) || `Field must not exceed ${320} characters`),
    keywords: Yup.string().max(300, t('validation.maxLength', { max: 300 }) || `Field must not exceed ${300} characters`),
    canonicalUrl: Yup.string().url(t('page.seo.form.urlInvalid') || 'Enter a full address, starting with https://'),
    ogTitle: Yup.string().max(120, t('validation.maxLength', { max: 120 }) || `Field must not exceed ${120} characters`),
    ogDescription: Yup.string().max(320, t('validation.maxLength', { max: 320 }) || `Field must not exceed ${320} characters`),
    priority: Yup.string().test('priority', t('page.seo.form.priorityInvalid') || 'Enter a number from 0 to 1', (value) => isValidPriority(value)),
    jsonLd: Yup.string().test('json', t('page.seo.form.jsonLdInvalid') || 'This is not valid JSON', (value) => isValidJson(value)),
  })

/** Props for SeoPageForm: what it starts from, and how it is saved. */
interface SeoPageFormProps {
  initialValues: SeoPageFormValues
  /** Saves the record; resolves with the API's error, if any. */
  onSave: (fields: SeoPageFields) => Promise<{ error?: ApiError }>
  isSaving: boolean
  /** The toast shown once the record is saved. */
  successMessage: string
}

/**
 * The create and update form of one page's SEO record, in four sections: Basics (the path, language,
 * title, description and keywords), Social sharing (the Open Graph title, description and image, and
 * the X card), Indexing & sitemap (noindex, nofollow, the canonical address and the sitemap entry), and
 * Structured data (JSON-LD, checked as JSON before it is sent). A save clears the public pages' cached
 * metadata and returns to the list; a record that already exists for the path and language is refused
 * on the path field itself.
 */
export const SeoPageForm = ({ initialValues, onSave, isSaving, successMessage }: SeoPageFormProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const dictionary = useContext(TranslationContext)
  const validationSchema = createValidationSchema(t)

  const cultureOptions = [
    { value: ALL_CULTURES, label: t('page.seo.allLanguages') || 'All languages' },
    ...dictionary.languages.map((language) => ({ value: language.code, label: language.name })),
  ]
  const frequencyOptions = [
    { value: NO_FREQUENCY, label: t('page.seo.form.frequencyNone') || 'Not stated' },
    ...CHANGE_FREQUENCIES.map((frequency) => ({ value: frequency, label: t(`page.seo.form.frequency.${frequency}`) || frequency })),
  ]
  const twitterCardOptions = [
    { value: 'summary_large_image', label: t('page.seo.form.twitterCardLarge') || 'Large image' },
    { value: 'summary', label: t('page.seo.form.twitterCardSummary') || 'Small image' },
  ]

  const onSubmit = async (values: SeoPageFormValues, helpers: FormikHelpers<SeoPageFormValues>) => {
    const result = await onSave(toSeoPageFields(values))
    if (result.error) {
      if (getErrorCode(result.error) === 'seoPageAlreadyExists') {
        await helpers.setFieldTouched('path', true, false)
        helpers.setFieldError('path', t('error.server.seoPageAlreadyExists') || 'This page already has SEO settings for that language.')
        document.getElementById(pathInputId)?.focus()
        return
      }
      apiErrorAlert(result.error)
      return
    }

    await revalidatePublicCache(['seo'])
    successToast.fire({ text: successMessage })
    router.push(adminPath('/seo'))
  }

  return (
    <Formik<SeoPageFormValues> initialValues={initialValues} validationSchema={validationSchema} onSubmit={onSubmit}>
      {({ values }) => (
        <Form noValidate>
          <FormSection title={t('page.seo.form.basics') || 'Basics'} description={t('page.seo.form.basicsHelp') || 'Which page this is, and how it appears in search results.'}>
            <FormInput
              id={pathInputId}
              name="path"
              label={t('table.columns.path') || 'Path'}
              placeholder="/about"
              dir="ltr"
              required={true}
              autoFocus={true}
              hint={t('page.seo.form.pathHelp') || 'The page address without the language, such as / or /about.'}
            />
            <FormSelect name="culture" label={t('table.columns.language') || 'Language'} options={cultureOptions} clearable={false} searchable={false} />
            <div className="min-w-0">
              <FormInput
                name="title"
                label={t('table.columns.title') || 'Title'}
                hint={t('page.seo.form.titleHelp') || 'Shown in the browser tab and search results, through the site title template.'}
                aria-describedby={titleCounterId}
              />
              <p id={titleCounterId} className="mt-1 text-xs text-muted-foreground tabular-nums">
                {t('page.seo.form.titleCharacters', { count: values.title.length }) || `${values.title.length} characters (about 60 show in results)`}
              </p>
            </div>
            <FormInput name="keywords" label={t('page.seo.form.keywords') || 'Keywords'} hint={t('page.seo.form.keywordsHelp') || 'Separated by commas.'} />
            <div className="sm:col-span-2">
              <FormTextarea name="description" label={t('table.columns.description') || 'Description'} rows={3} aria-describedby={descriptionCounterId} />
              <p id={descriptionCounterId} className="mt-1.5 text-xs text-muted-foreground tabular-nums">
                {t('page.seo.form.characters', { count: values.description.length }) || `${values.description.length} characters (about 155 show in results)`}
              </p>
            </div>
          </FormSection>

          <FormSection title={t('page.seo.form.social') || 'Social sharing'} description={t('page.seo.form.socialHelp') || 'How the page looks when someone shares a link to it. Blank fields use the basics.'}>
            <FormInput name="ogTitle" label={t('page.seo.form.ogTitle') || 'Sharing title'} />
            <FormSelect name="twitterCard" label={t('page.seo.form.twitterCard') || 'X (Twitter) card'} options={twitterCardOptions} clearable={false} searchable={false} />
            <div className="sm:col-span-2">
              <FormTextarea name="ogDescription" label={t('page.seo.form.ogDescription') || 'Sharing description'} rows={2} />
            </div>
            <div className="sm:col-span-2">
              <FormImageUpload name="ogImageFileId" label={t('page.seo.form.ogImage') || 'Sharing image'} hint={t('page.seo.form.ogImageHelp') || 'Blank uses the site default. 1200 x 630 pixels works best.'} />
            </div>
          </FormSection>

          <FormSection title={t('page.seo.form.indexing') || 'Indexing & sitemap'} description={t('page.seo.form.indexingHelp') || 'Whether search engines list the page, and how the sitemap describes it.'}>
            <div className="flex flex-col gap-3 sm:col-span-2">
              <FormCheckbox name="noIndex" label={t('page.seo.form.noIndex') || 'Hide from search results (noindex)'} />
              <FormCheckbox name="noFollow" label={t('page.seo.form.noFollow') || "Don't follow links on this page (nofollow)"} />
              <FormCheckbox name="includeInSitemap" label={t('page.seo.form.includeInSitemap') || 'Include in the sitemap'} />
            </div>
            <div className="sm:col-span-2">
              <FormInput name="canonicalUrl" label={t('page.seo.form.canonicalUrl') || 'Canonical address'} dir="ltr" type="url" placeholder="https://" hint={t('page.seo.form.canonicalUrlHelp') || "Blank uses the page's own address."} />
            </div>
            <FormSelect name="changeFrequency" label={t('page.seo.form.changeFrequency') || 'Change frequency'} options={frequencyOptions} clearable={false} searchable={false} disabled={!values.includeInSitemap} />
            <FormInput name="priority" label={t('page.seo.form.priority') || 'Priority'} inputMode="decimal" placeholder="0.5" disabled={!values.includeInSitemap} hint={t('page.seo.form.priorityHelp') || 'From 0 to 1, relative to your other pages.'} />
          </FormSection>

          <FormSection columns={1} title={t('page.seo.form.structuredData') || 'Structured data'} description={t('page.seo.form.structuredDataHelp') || 'A JSON-LD document describing the page to search engines, such as an Organization or a Product.'}>
            <FormTextarea name="jsonLd" label="JSON-LD" rows={8} dir="ltr" className="font-mono text-xs" placeholder={'{\n  "@context": "https://schema.org",\n  "@type": "Organization"\n}'} />
          </FormSection>

          <FormActions>
            <Button type="button" variant="outline" onClick={() => router.push(adminPath('/seo'))} disabled={isSaving}>
              {t('common.cancel') || 'Cancel'}
            </Button>
            <Button type="submit" isLoading={isSaving}>
              {t('common.save') || 'Save'}
            </Button>
          </FormActions>
        </Form>
      )}
    </Formik>
  )
}
