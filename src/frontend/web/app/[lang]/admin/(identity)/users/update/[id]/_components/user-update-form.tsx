'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { useUserUpdateMutation, useUserGetQuery } from '@/store/api/identity/users/users-api'
import { useLazyRoleListQuery } from '@/store/api/identity/roles/roles-api'
import { Form, Formik } from 'formik'
import { Button, ApiErrorMessages, Loading } from '@/components/ui'
import { FormInput, FormCheckbox, FormLazyMultiSelect } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the user update form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    username: Yup.string(),
    firstName: Yup.string()
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    lastName: Yup.string()
      .min(3, t('validation.minLength', { min: 3 }))
      .max(50, t('validation.maxLength', { max: 50 })),
    isActive: Yup.boolean()
      .required(),
    roles: Yup.array().of(Yup.string())
      .required(t('validation.required'))
      .min(1, t('validation.atLeastOneSelected')),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Props for the UserUpdateForm, supplying the id of the user being edited.
 */
interface UserUpdateFormProps {
  userId: string
}

/**
 * Interactive client-side form for editing an existing user's profile fields, role assignments, and active state, populated from the API and submitted via the user-update mutation.
 */
export const UserUpdateForm = ({ userId }: UserUpdateFormProps) => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const validationSchema = createValidationSchema(t)
  const { data: userData, isLoading: isLoadingUser, error: getUserError } = useUserGetQuery({ id: userId })
  const [updateUser, { isLoading: isUserSaving }] = useUserUpdateMutation()

  if (isLoadingUser) {
    return (
      <div className="flex justify-center items-center">
        <Loading />
      </div>
    )
  }

  if (getUserError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={getUserError} />
      </div>
    )
  }

  if (!isLoadingUser && !getUserError && !userData) {
    return (
      <div className="flex justify-center items-center">
        {t('error.server.userNotFound')}
      </div>
    )
  }

  const onSubmit = async (data: FormValues) => {
    const { username: _username, ...updateData } = data
    const result = await updateUser({
      ...updateData,
      id: userId,
      roles: data.roles.filter((role): role is string => role !== undefined),
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.users.updateSuccess'),
    })
    router.push('/admin/users/list')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        username: userData.username,
        firstName: userData.firstName || '',
        lastName: userData.lastName || '',
        isActive: userData.isActive,
        roles: userData.roles,
      }}
      validationSchema={validationSchema}
      onSubmit={onSubmit}
    >
      {() => (
        <Form noValidate className="grid grid-cols-1 gap-4">
          <FormInput
            name="username"
            label={t('form.label.username')}
            placeholder={t('form.placeholder.username')}
            disabled={true}
          />
          <FormInput
            name="firstName"
            label={t('form.label.firstName')}
            placeholder={t('form.placeholder.firstName')}
            autoFocus={true}
          />
          <FormInput
            name="lastName"
            label={t('form.label.lastName')}
            placeholder={t('form.placeholder.lastName')}
          />
          <FormLazyMultiSelect
            name="roles"
            label={t('form.label.roles')}
            placeholder={t('form.placeholder.roles')}
            selectedItemIds={userData.roles}
            useLazyQuery={useLazyRoleListQuery}
            getLabel={(role) => role.name}
            getValue={(role) => role.id}
            size="sm"
            pageSize={20}
            required={true}
          />
          <FormCheckbox
            name="isActive"
            label={t('form.label.isActive')}
          />
          <div className="flex justify-end gap-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/users/list')}
              disabled={isUserSaving || isLoadingUser}
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              disabled={isUserSaving || isLoadingUser}
              isLoading={isUserSaving}
            >
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
