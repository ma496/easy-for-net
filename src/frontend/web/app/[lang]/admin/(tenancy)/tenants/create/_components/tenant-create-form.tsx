'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useTenantCreateMutation, useLazyEditionListQuery, EditionListDto, EditionListRequest } from '@/store/api/tenancy'
import { Form, Formik } from 'formik'
import { Button } from '@/components/ui'
import { FormInput, FormLazySelect } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the tenant create form using the supplied translation function for error messages.
 * The identifier rules mirror `TenantValidator` on the API: lower-case letters, digits and single hyphens, beginning and
 * ending with a letter or a digit, so a value that passes here is one the API accepts.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    name: Yup.string()
      .trim()
      .required(t('validation.required') || 'Field is required')
      .min(2, t('validation.minLength', { min: 2 }) || `Field must be at least ${2} characters`)
      .max(100, t('validation.maxLength', { max: 100 }) || `Field must not exceed ${100} characters`),
    identifier: Yup.string()
      .trim()
      .required(t('validation.required') || 'Field is required')
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`)
      .matches(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, t('validation.tenantIdentifier') || 'Use lower-case letters, digits and single hyphens, starting and ending with a letter or digit'),
    // Optional: a tenant on no plan is legitimate, and falls through to whatever the deployment and
    // the feature definitions declare.
    editionId: Yup.string(),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Interactive client-side form for creating a tenant, with display-name and identifier fields, validation, and
 * navigation back to the tenants list on success. A duplicate identifier is reported from the API's error code and
 * shown as a translated message by `apiErrorAlert`, never as a raw code.
 */
export const TenantCreateForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [createTenant, { isLoading: isSavingTenant }] = useTenantCreateMutation()
  const router = useLocalizedRouter()

  const onSubmit = async (data: FormValues) => {
    const result = await createTenant({
      name: data.name,
      identifier: data.identifier,
      editionId: data.editionId || null,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.tenants.createSuccess') || 'Tenant created successfully',
    })
    router.push('/admin/tenants')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: '',
        identifier: '',
        editionId: '',
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <FormInput
            name="name"
            label={t('form.label.tenantName') || 'Tenant Name'}
            placeholder={t('form.placeholder.tenantName') || 'Enter tenant name'}
            autoFocus={true}
            required={true}
          />
          <FormInput
            name="identifier"
            label={t('form.label.tenantIdentifier') || 'Identifier'}
            placeholder={t('form.placeholder.tenantIdentifier') || 'Enter identifier (for example acme)'}
            required={true}
          />
          <FormLazySelect<EditionListDto, EditionListRequest>
            name="editionId"
            className="sm:col-span-2"
            label={t('navigation.editions') || 'Editions'}
            placeholder={t('form.placeholder.edition') || 'Select an edition'}
            useLazyQuery={useLazyEditionListQuery}
            getLabel={(edition) => edition.name}
            getValue={(edition) => edition.id}
            pageSize={20}
          />
          <div className="mt-1 flex justify-end gap-2 border-t border-border pt-5 sm:col-span-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/tenants')}
              disabled={isSavingTenant}
            >
              {t('common.cancel') || 'Cancel'}
            </Button>
            <Button
              type="submit"
              isLoading={isSavingTenant}
            >
              {t('common.submit') || 'Submit'}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
