'use client'

import { useTranslation } from '@/i18n'
import { ApiErrorMessages, Loader } from '@/components/ui'
import { FormState } from '@/app/[lang]/admin/_components/form-layout'
import { useSeoPageCreateMutation, useSeoPageGetQuery, useSeoPageUpdateMutation } from '@/store/api/seo'
import { SeoPageForm } from './seo-page-form'
import { emptySeoPageFormValues, seoPageFormValues } from './seo-page-form-values'

/** The create screen's form: an empty record for the home page, in every language. */
export const SeoPageCreateForm = () => {
  const { t } = useTranslation()
  const [createPage, { isLoading }] = useSeoPageCreateMutation()

  return (
    <SeoPageForm
      initialValues={emptySeoPageFormValues}
      isSaving={isLoading}
      successMessage={t('page.seo.createSuccess') || 'SEO settings created'}
      onSave={async (fields) => {
        const result = await createPage(fields)
        return { error: result.error }
      }}
    />
  )
}

/** The update screen's form: the stored record, with its loading, failure and not-found states. */
export const SeoPageUpdateForm = ({ id }: { id: string }) => {
  const { t } = useTranslation()
  const { data, isLoading, error } = useSeoPageGetQuery({ id })
  const [updatePage, { isLoading: isSaving }] = useSeoPageUpdateMutation()

  if (isLoading) {
    return (
      <FormState>
        <Loader size="lg" />
      </FormState>
    )
  }

  if (error) {
    return (
      <FormState>
        <ApiErrorMessages error={error} className="w-full" dismissible={false} />
      </FormState>
    )
  }

  if (!data) {
    return <FormState>{t('page.seo.notFound') || 'These SEO settings no longer exist'}</FormState>
  }

  return (
    <SeoPageForm
      initialValues={seoPageFormValues(data)}
      isSaving={isSaving}
      successMessage={t('page.seo.updateSuccess') || 'SEO settings saved'}
      onSave={async (fields) => {
        const result = await updatePage({ ...fields, id })
        return { error: result.error }
      }}
    />
  )
}
