'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useTenantGetQuery, useTenantUpdateMutation, useLazyEditionListQuery, EditionListDto, EditionListRequest } from '@/store/api/tenancy'
import { Form, Formik } from 'formik'
import { Button, ApiErrorMessages, Loader } from '@/components/ui'
import { FormInput, FormLazySelect } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the tenant update form using the supplied translation function for error messages.
 * The identifier rules mirror `TenantValidator` on the API, so a value that passes here is one the API accepts.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    name: Yup.string()
      .trim()
      .required(t('validation.required'))
      .min(2, t('validation.minLength', { min: 2 }))
      .max(100, t('validation.maxLength', { max: 100 })),
    identifier: Yup.string()
      .trim()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 }))
      .matches(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, t('validation.tenantIdentifier')),
    // Optional, and clearing it takes the tenant off its plan rather than being a missing value.
    editionId: Yup.string(),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Props for the TenantUpdateForm, supplying the id of the tenant being edited.
 */
interface TenantUpdateFormProps {
  tenantId: string
}

/**
 * Interactive client-side form for editing an existing tenant's display name and identifier, populated from the API
 * and submitted back through the tenant-update mutation. The system-created tenant is shown as unmodifiable rather
 * than editable, since the API refuses to rename it (AC-011).
 */
export const TenantUpdateForm = ({ tenantId }: TenantUpdateFormProps) => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [updateTenant, { isLoading: isSavingTenant }] = useTenantUpdateMutation()
  const { data: tenantData, isLoading: isLoadingTenant, error: tenantGetError } = useTenantGetQuery({ id: tenantId })
  const router = useLocalizedRouter()

  if (isLoadingTenant) {
    return (
      <div className="flex justify-center items-center">
        <Loader />
      </div>
    )
  }

  if (tenantGetError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={tenantGetError} />
      </div>
    )
  }

  if (!isLoadingTenant && !tenantGetError && !tenantData) {
    return (
      <div className="flex justify-center items-center">
        {t('page.tenants.notFound')}
      </div>
    )
  }

  if (tenantData.systemCreated) {
    return (
      <div className="flex justify-center items-center">
        {t('error.server.systemCreatedTenantCannotBeModified')}
      </div>
    )
  }

  const onSubmit = async (data: FormValues) => {
    const result = await updateTenant({
      id: tenantId,
      name: data.name,
      identifier: data.identifier,
      editionId: data.editionId || null,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.tenants.updateSuccess'),
    })
    router.push('/admin/tenants/list')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: tenantData.name,
        identifier: tenantData.identifier,
        editionId: tenantData.editionId ?? '',
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-4">
          <FormInput
            name="name"
            label={t('form.label.tenantName')}
            placeholder={t('form.placeholder.tenantName')}
            autoFocus={true}
            required={true}
          />
          <FormInput
            name="identifier"
            label={t('form.label.tenantIdentifier')}
            placeholder={t('form.placeholder.tenantIdentifier')}
            required={true}
          />
          <FormLazySelect<EditionListDto, EditionListRequest>
            name="editionId"
            label={t('navigation.editions')}
            placeholder={t('form.placeholder.edition')}
            useLazyQuery={useLazyEditionListQuery}
            getLabel={(edition) => edition.name}
            getValue={(edition) => edition.id}
            selectedItemId={tenantData.editionId ?? undefined}
            pageSize={20}
          />
          <div className="flex justify-end gap-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/tenants/list')}
              disabled={isSavingTenant}
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              isLoading={isSavingTenant}
            >
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
