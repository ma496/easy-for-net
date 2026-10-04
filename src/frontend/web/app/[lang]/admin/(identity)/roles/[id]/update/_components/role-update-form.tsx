'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useRoleUpdateMutation, useRoleGetQuery } from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { Button, ApiErrorMessages, Loader } from '@/components/ui'
import { FormInput, FormTextarea } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'
import { FormSection, FormActions, FormState } from '../../../../_components/form-layout'

/**
 * Builds a Yup validation schema for the role update form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    name: Yup.string()
      .required(t('validation.required'))
      .min(2, t('validation.minLength', { min: 2 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    description: Yup.string()
      .min(10, t('validation.minLength', { min: 10 }))
      .max(255, t('validation.maxLength', { max: 255 })),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Props for the RoleUpdateForm, supplying the id of the role being edited.
 */
interface RoleUpdateFormProps {
  roleId: string
}

/**
 * Interactive client-side form for editing an existing role's name and description, populated from the API and submitted back through the role-update mutation.
 */
export const RoleUpdateForm = ({ roleId }: RoleUpdateFormProps) => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [updateRole, { isLoading: isSavingRole }] = useRoleUpdateMutation()
  const { data: roleData, isLoading: isLoadingRole, error: roleGetError } = useRoleGetQuery({ id: roleId })
  const router = useLocalizedRouter()

  if (isLoadingRole) {
    return (
      <FormState>
        <Loader size="lg" />
      </FormState>
    )
  }

  if (roleGetError) {
    return (
      <FormState>
        <ApiErrorMessages error={roleGetError} className="w-full" dismissible={false} />
      </FormState>
    )
  }

  if (!isLoadingRole && !roleGetError && !roleData) {
    return (
      <FormState>
        {t('page.roles.notFound')}
      </FormState>
    )
  }

  const onSubmit = async (data: FormValues) => {
    const result = await updateRole({
      ...data,
      id: roleId,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.roles.updateSuccess'),
    })
    router.push('/admin/roles')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: roleData.name,
        description: roleData.description || '',
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate>
          <FormSection columns={1}>
            <FormInput
              name="name"
              label={t('form.label.roleName')}
              placeholder={t('form.placeholder.roleName')}
              autoFocus={true}
              required={true}
            />
            <FormTextarea
              name="description"
              label={t('form.label.roleDescription')}
              placeholder={t('form.placeholder.roleDescription')}
              rows={4}
            />
          </FormSection>
          <FormActions>
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/roles')}
              disabled={isSavingRole}
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              isLoading={isSavingRole}
            >
              {t('common.submit')}
            </Button>
          </FormActions>
        </Form>
      )}
    </Formik>
  )
}
