'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useTenantGetQuery, useTenantUpdateMutation, useLazyEditionListQuery, EditionListDto, EditionListRequest } from '@/store/api/tenancy'
import { Form, Formik } from 'formik'
import { Button, ApiErrorMessages, Loader } from '@/components/ui'
import { FormInput, FormLazySelect } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'
import { Building2, Lock } from 'lucide-react'

/**
 * Builds a Yup validation schema for the tenant update form using the supplied translation function for error messages.
 * The identifier rules mirror `TenantValidator` on the API, so a value that passes here is one the API accepts.
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
 * than editable, since the API refuses to rename it.
 */
export const TenantUpdateForm = ({ tenantId }: TenantUpdateFormProps) => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [updateTenant, { isLoading: isSavingTenant }] = useTenantUpdateMutation()
  const { data: tenantData, isLoading: isLoadingTenant, error: tenantGetError } = useTenantGetQuery({ id: tenantId })
  const router = useLocalizedRouter()

  if (isLoadingTenant) {
    return (
      <div className="flex items-center justify-center py-12">
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
      <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
        <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
          <Building2 className="size-5" />
        </div>
        <p className="max-w-sm text-sm text-muted-foreground">{t('page.tenants.notFound') || 'Tenant not found'}</p>
      </div>
    )
  }

  if (tenantData.systemCreated) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
        <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
          <Lock className="size-5" />
        </div>
        <p className="max-w-sm text-sm text-muted-foreground">{t('error.server.systemCreatedTenantCannotBeModified') || 'System-created tenant cannot be modified'}</p>
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
      text: t('page.tenants.updateSuccess') || 'Tenant updated successfully',
    })
    router.push('/admin/tenants')
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
            selectedItemId={tenantData.editionId ?? undefined}
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
