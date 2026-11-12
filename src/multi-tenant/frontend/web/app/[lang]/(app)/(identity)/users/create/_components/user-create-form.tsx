'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useUserCreateMutation, useLazyRoleListQuery, RoleListRequest, RoleListDto } from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { FormPasswordInput, FormInput, FormCheckbox, FormLazyMultiSelect } from '@/components/ui/form'
import { Button } from '@/components/ui'
import { apiErrorAlert, successToast } from '@/lib/utils'
import { FormSection, FormActions } from '../../../_components/form-layout'

/**
 * Builds a Yup validation schema for the user create form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    email: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .email(t('validation.invalidEmail') || 'Invalid email'),
    firstName: Yup.string()
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    lastName: Yup.string()
      .min(3, t('validation.minLength', { min: 3 }) || `Field must be at least ${3} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    password: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .min(8, t('validation.minLength', { min: 8 }) || `Field must be at least ${8} characters`)
      .max(50, t('validation.maxLength', { max: 50 }) || `Field must not exceed ${50} characters`),
    confirmPassword: Yup.string()
      .required(t('validation.required') || 'Field is required')
      .oneOf([Yup.ref('password')], t('validation.mustMatch', { otherField: t('form.label.password') || 'Password' }) || `Field must match with ${t('form.label.password') || 'Password'}`),
    isActive: Yup.boolean().required(),
    roles: Yup.array().of(Yup.string())
      .required(t('validation.required') || 'Field is required')
      .min(1, t('validation.atLeastOneSelected') || 'At least one item must be selected'),
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
      text: t('page.users.createSuccess') || 'User created successfully',
    })
    router.push('/users')
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
        <Form noValidate>
          <FormSection title={t('page.users.accountDetails') || 'Account details'}>
            <FormInput
              name="username"
              label={t('form.label.username') || 'Username'}
              placeholder={t('form.placeholder.username') || 'Enter username'}
              autoFocus={true}
              required={true}
            />
            <FormInput
              name="email"
              type="email"
              label={t('form.label.email') || 'Email'}
              placeholder={t('form.placeholder.email') || 'Enter email'}
              required={true}
            />
            <FormInput
              name="firstName"
              label={t('form.label.firstName') || 'First Name'}
              placeholder={t('form.placeholder.firstName') || 'Enter first name'}
            />
            <FormInput
              name="lastName"
              label={t('form.label.lastName') || 'Last Name'}
              placeholder={t('form.placeholder.lastName') || 'Enter last name'}
            />
          </FormSection>
          <FormSection title={t('form.label.password') || 'Password'} description={t('page.users.passwordDescription') || 'The password this user signs in with.'}>
            <FormPasswordInput
              name="password"
              label={t('form.label.password') || 'Password'}
              placeholder={t('form.placeholder.password') || 'Enter password'}
              required={true}
            />
            <FormPasswordInput
              name="confirmPassword"
              label={t('form.label.confirmPassword') || 'Confirm Password'}
              placeholder={t('form.placeholder.confirmPassword') || 'Enter confirm password'}
              required={true}
            />
          </FormSection>
          <FormSection columns={1}>
            <FormLazyMultiSelect<RoleListDto, RoleListRequest>
              name="roles"
              label={t('form.label.roles') || 'Roles'}
              placeholder={t('form.placeholder.roles') || 'Select roles'}
              useLazyQuery={useLazyRoleListQuery}
              getLabel={(role) => role.name}
              getValue={(role) => role.id}
              size="sm"
              pageSize={20}
              required={true}
            />
            <div className="rounded-lg border border-border px-4 py-3">
              <FormCheckbox
                name="isActive"
                label={t('form.label.isActive') || 'Active'}
              />
              <p className="mt-1 ps-5 text-xs text-muted-foreground">{t('page.users.isActiveDescription') || 'An inactive user cannot sign in.'}</p>
            </div>
          </FormSection>
          <FormActions>
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/users')}
              disabled={isCreatingUser}
            >
              {t('common.cancel') || 'Cancel'}
            </Button>
            <Button
              type="submit"
              isLoading={isCreatingUser}
            >
              {t('common.submit') || 'Submit'}
            </Button>
          </FormActions>
        </Form>
      )}
    </Formik>
  )
}
