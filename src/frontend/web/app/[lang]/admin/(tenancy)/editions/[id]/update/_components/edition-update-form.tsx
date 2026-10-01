'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useEditionGetQuery, useEditionUpdateMutation } from '@/store/api/tenancy'
import { Form, Formik } from 'formik'
import { Button, ApiErrorMessages, Loader } from '@/components/ui'
import { FormInput, FormTextarea } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the edition update form. The bounds mirror
 * `EditionValidationRules` on the API, so a value that passes here is one the API accepts.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    name: Yup.string()
      .trim()
      .required(t('validation.required'))
      .min(2, t('validation.minLength', { min: 2 }))
      .max(128, t('validation.maxLength', { max: 128 })),
    description: Yup.string().trim().max(512, t('validation.maxLength', { max: 512 })),
    displayOrder: Yup.number()
      .typeError(t('validation.number'))
      .min(0, t('validation.min', { min: 0 }))
      .required(t('validation.required')),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Props for the EditionUpdateForm, supplying the id of the plan being edited.
 */
interface EditionUpdateFormProps {
  editionId: string
}

/**
 * Interactive client-side form for renaming a plan or changing how it is described and ordered. What
 * the plan is worth is not edited here — its entitlements live on its own features screen, so a
 * rename never silently alters what the tenants on it are entitled to.
 */
export const EditionUpdateForm = ({ editionId }: EditionUpdateFormProps) => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [updateEdition, { isLoading: isSavingEdition }] = useEditionUpdateMutation()
  const { data: editionData, isLoading: isLoadingEdition, error: editionGetError } = useEditionGetQuery({ id: editionId })
  const router = useLocalizedRouter()

  if (isLoadingEdition) {
    return (
      <div className="flex justify-center items-center">
        <Loader />
      </div>
    )
  }

  if (editionGetError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={editionGetError} />
      </div>
    )
  }

  if (!isLoadingEdition && !editionGetError && !editionData) {
    return (
      <div className="flex justify-center items-center">
        {t('page.editions.notFound')}
      </div>
    )
  }

  const onSubmit = async (data: FormValues) => {
    const result = await updateEdition({
      id: editionId,
      name: data.name,
      description: data.description || null,
      displayOrder: data.displayOrder,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.editions.updateSuccess'),
    })
    router.push('/admin/editions/list')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: editionData.name,
        description: editionData.description ?? '',
        displayOrder: editionData.displayOrder,
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-4">
          <FormInput
            name="name"
            label={t('form.label.editionName')}
            placeholder={t('form.placeholder.editionName')}
            autoFocus={true}
            required={true}
          />
          <FormTextarea
            name="description"
            label={t('form.label.editionDescription')}
            placeholder={t('form.placeholder.editionDescription')}
          />
          <FormInput
            name="displayOrder"
            type="number"
            label={t('form.label.editionDisplayOrder')}
            required={true}
          />
          <div className="flex justify-end gap-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/editions/list')}
              disabled={isSavingEdition}
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              isLoading={isSavingEdition}
            >
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
