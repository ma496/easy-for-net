'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useRoleCreateMutation } from '@/store/api/identity/roles/roles-api'
import { Form, Formik } from 'formik'
import { Button } from '@/components/ui'
import { FormInput, FormTextarea } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the role create form using the supplied translation function for error messages.
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
 * Interactive client-side form for creating a new role, with name and description fields, validation, and navigation back to the roles list on success.
 */
export const RoleCreateForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [createRole, { isLoading: isSavingRole }] = useRoleCreateMutation()
  const router = useLocalizedRouter()

  const onSubmit = async (data: FormValues) => {
    const result = await createRole({
      ...data,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.roles.createSuccess'),
    })
    router.push('/admin/roles/list')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: '',
        description: '',
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-4">
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
          <div className="flex justify-end gap-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/roles/list')}
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
          </div>
        </Form>
      )}
    </Formik>
  )
}
