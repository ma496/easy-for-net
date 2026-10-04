'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useEditionCreateMutation } from '@/store/api/tenancy'
import { Form, Formik } from 'formik'
import { Button } from '@/components/ui'
import { FormInput, FormTextarea } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the edition create form. The bounds mirror
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
 * Interactive client-side form for creating an edition — a plan the platform can put tenants on. The
 * plan is created carrying no entitlements at all; what it is worth is set afterwards on its features
 * screen, which keeps naming a plan and pricing it two separate decisions.
 */
export const EditionCreateForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [createEdition, { isLoading: isSavingEdition }] = useEditionCreateMutation()
  const router = useLocalizedRouter()

  const onSubmit = async (data: FormValues) => {
    const result = await createEdition({
      name: data.name,
      description: data.description || null,
      displayOrder: data.displayOrder,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.editions.createSuccess'),
    })
    router.push('/admin/editions')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: '',
        description: '',
        displayOrder: 0,
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <FormInput
            name="name"
            label={t('form.label.editionName')}
            placeholder={t('form.placeholder.editionName')}
            autoFocus={true}
            required={true}
          />
          <FormInput
            name="displayOrder"
            type="number"
            label={t('form.label.editionDisplayOrder')}
            required={true}
          />
          <FormTextarea
            className="sm:col-span-2 [&_textarea]:h-auto [&_textarea]:min-h-24 [&_textarea]:py-2"
            rows={4}
            name="description"
            label={t('form.label.editionDescription')}
            placeholder={t('form.placeholder.editionDescription')}
          />
          <div className="mt-1 flex justify-end gap-2 border-t border-border pt-5 sm:col-span-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/editions')}
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
