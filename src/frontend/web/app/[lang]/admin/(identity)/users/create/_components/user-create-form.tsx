'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useUserCreateMutation, useLazyRoleListQuery, RoleListRequest, RoleListDto } from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { FormPasswordInput, FormInput, FormCheckbox, FormLazyMultiSelect } from '@/components/ui/form'
import { Button } from '@/components/ui'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the user create form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    email: Yup.string()
      .required(t('validation.required'))
      .email(t('validation.invalidEmail')),
    firstName: Yup.string()
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    lastName: Yup.string()
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    password: Yup.string()
      .required(t('validation.required'))
      .min(8, t('validation.minLength', { min: 8 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    confirmPassword: Yup.string()
      .required(t('validation.required'))
      .oneOf([Yup.ref('password')], t('validation.mustMatch', { otherField: t('form.label.password') })),
    isActive: Yup.boolean().required(),
    roles: Yup.array().of(Yup.string())
      .required(t('validation.required'))
      .min(1, t('validation.atLeastOneSelected')),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Interactive client-side form for creating a new user, including credentials, profile fields, role assignment via a lazy multi-select, and active-state toggle.
 */
export const UserCreateForm = () => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [createUser, { isLoading: isCreatingUser }] = useUserCreateMutation()
  const router = useLocalizedRouter()

  const onSubmit = async (data: FormValues) => {
    const result = await createUser({
      ...data,
      roles: data.roles.filter((role): role is string => role !== undefined),
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.users.createSuccess'),
    })
    router.push('/admin/users/list')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        username: '',
        email: '',
        firstName: '',
        lastName: '',
        password: '',
        confirmPassword: '',
        isActive: true,
        roles: [],
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormInput
            name="username"
            label={t('form.label.username')}
            placeholder={t('form.placeholder.username')}
            autoFocus={true}
            required={true}
          />
          <FormInput
            name="email"
            type="email"
            label={t('form.label.email')}
            placeholder={t('form.placeholder.email')}
            required={true}
          />
          <FormInput
            name="firstName"
            label={t('form.label.firstName')}
            placeholder={t('form.placeholder.firstName')}
          />
          <FormInput
            name="lastName"
            label={t('form.label.lastName')}
            placeholder={t('form.placeholder.lastName')}
          />
          <FormPasswordInput
            name="password"
            label={t('form.label.password')}
            placeholder={t('form.placeholder.password')}
            required={true}
          />
          <FormPasswordInput
            name="confirmPassword"
            label={t('form.label.confirmPassword')}
            placeholder={t('form.placeholder.confirmPassword')}
            required={true}
          />
          <div className="sm:col-span-2">
            <FormLazyMultiSelect<RoleListDto, RoleListRequest>
              name="roles"
              label={t('form.label.roles')}
              placeholder={t('form.placeholder.roles')}
              useLazyQuery={useLazyRoleListQuery}
              getLabel={(role) => role.name}
              getValue={(role) => role.id}
              size="sm"
              pageSize={20}
              required={true}
            />
          </div>
          <div className="sm:col-span-2">
            <FormCheckbox
              name="isActive"
              label={t('form.label.isActive')}
            />
          </div>
          <div className="flex justify-end gap-4 sm:col-span-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/users/list')}
              disabled={isCreatingUser}
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              isLoading={isCreatingUser}
            >
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
