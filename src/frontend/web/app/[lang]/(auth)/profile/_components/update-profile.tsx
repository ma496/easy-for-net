'use client'

import { useGetUserProfileQuery, useLazyGetUserInfoQuery, useUpdateProfileMutation } from '@/store/api/identity/account/account-api'
import { FormInput, FileUpload } from '@/components/ui/form'
import { useAppDispatch } from '@/store/hooks'
import { setUserInfo } from '@/store/slices/authSlice'
import { Button, ApiErrorMessages, IconButton } from '@/components/ui'
import { Mail, Pencil, Trash2, User } from 'lucide-react'
import * as Yup from 'yup'
import { Formik, Form } from 'formik'
import { useTranslation } from '@/i18n'
import { UpdateProfileRequest } from '@/store/api/identity/account/account-dtos'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the update-profile form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object({
    firstName: Yup.string().when('lastName', {
      is: (lastName: string) => lastName && lastName.length > 0,
      then: (schema) => schema.required(t('validation.required')),
      otherwise: (schema) => schema.optional(),
    }),
    lastName: Yup.string().optional(),
    email: Yup.string()
      .required(t('validation.required'))
      .email(t('validation.invalidEmail')),
    image: Yup.string().optional(),
  })
}

/**
 * Interactive client-side form that lets the authenticated user view and update their personal profile information.
 * Supports avatar upload/deletion and refreshes the cached user info on a successful save.
 */
export const UpdateProfile = () => {
  const dispatch = useAppDispatch()
  const [updateProfile, { isLoading: isUpdatingProfile }] = useUpdateProfileMutation()
  const { data: userProfile, isLoading: isLoadingUserProfile, error: getUserProfileError } = useGetUserProfileQuery()
  const [getUserInfo] = useLazyGetUserInfoQuery()
  const { t } = useTranslation()

  const validationSchema = createValidationSchema(t)
  type UpdateProfileFormValues = Yup.InferType<typeof validationSchema>

  if (isLoadingUserProfile) {
    return <div>{t('common.loading')}</div>
  }

  if (getUserProfileError) {
    return <ApiErrorMessages error={getUserProfileError} />
  }

  const handleSubmit = async (values: UpdateProfileFormValues) => {
    const result = await updateProfile(values as UpdateProfileRequest)

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    const userInfo = await getUserInfo()
    if (userInfo.data) {
      dispatch(setUserInfo(userInfo.data))
      successToast.fire({
        text: t('page.profile.updateSuccess'),
      })
    }
  }

  return (
    <Formik
      initialValues={{
        firstName: userProfile?.firstName || '',
        lastName: userProfile?.lastName || '',
        email: userProfile?.email || '',
        image: userProfile?.image || undefined,
      }}
      validationSchema={validationSchema}
      onSubmit={handleSubmit}
    >
      {({ setFieldValue, values }) => (
        <Form noValidate className="flex flex-col gap-4">
          <div className="mb-6 flex flex-col items-center gap-4">
            <FileUpload
              name="profile-image"
              accept="image/*"
              maxSizeBytes={10 * 1024 * 1024}
              fileName={values.image}
              onUploaded={(res) => {
                setFieldValue('image', res.fileName)
              }}
              onClear={() => {
                setFieldValue('image', undefined)
              }}
            >
              {({ open, isUploading, isDeleting, deleteFile, selectedFileUrl }) => (
                <div className="flex flex-col items-center gap-4">
                  <div className="h-24 w-24 overflow-hidden rounded-full">
                    <img
                      src={selectedFileUrl || '/assets/images/default-avatar.svg'}
                      alt={t('page.profile.altImage')}
                      className="h-full w-full object-cover"
                    />
                  </div>
                  <div className="flex gap-2">
                    <IconButton
                      variant="outline"
                      rounded="full"
                      onClick={open}
                      aria-label="Edit"
                      title="Edit"
                      icon={<Pencil className="h-4 w-4" />}
                      isLoading={isUploading}
                      disabled={isUploading || isDeleting}
                    />
                    <IconButton
                      variant="outline-danger"
                      rounded="full"
                      onClick={async () => {
                        const result = await confirmDeleteAlert({
                          title: t('page.profile.deleteAvatarTitle'),
                          text: t('page.profile.deleteAvatarConfirm'),
                        })
                        if (result.isConfirmed) {
                          await deleteFile()
                          setFieldValue('image', undefined)
                        }
                      }}
                      aria-label="Delete"
                      title="Delete"
                      icon={<Trash2 className="h-4 w-4" />}
                      isLoading={isDeleting}
                      disabled={isUploading || isDeleting || (!selectedFileUrl && !values.image)}
                    />
                  </div>
                </div>
              )}
            </FileUpload>
          </div>

          <div>
            <FormInput label={t('form.label.firstName')} name="firstName" type="text" placeholder={t('form.placeholder.firstName')} autoFocus={true} icon={<User size={18} />} />
          </div>

          <div>
            <FormInput label={t('form.label.lastName')} name="lastName" type="text" placeholder={t('form.placeholder.lastName')} icon={<User size={18} />} />
          </div>

          <div>
            <FormInput label={t('form.label.email')} name="email" type="email" placeholder={t('form.placeholder.email')} icon={<Mail size={18} />} required={true} />
          </div>

          <div className="flex justify-end">
            <Button type="submit" isLoading={isUpdatingProfile || isLoadingUserProfile}>
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
