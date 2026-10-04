'use client'

import { useGetUserProfileQuery, useLazyGetUserInfoQuery, useUpdateProfileMutation, UpdateProfileRequest } from '@/store/api/identity'
import { FormInput, FileUpload } from '@/components/ui/form'
import { useAppDispatch } from '@/store/hooks'
import { setUserInfo } from '@/store/slices'
import { Button, ApiErrorMessages, IconButton, Loader, LocalizedLink } from '@/components/ui'
import { Mail, Pencil, Trash2, User } from 'lucide-react'
import * as Yup from 'yup'
import { Formik, Form } from 'formik'
import { useTranslation } from '@/i18n'
import { apiErrorAlert, confirmDeleteAlert, successToast } from '@/lib/utils'
import Image from 'next/image'

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
    email: Yup.string().required(t('validation.required')).email(t('validation.invalidEmail')),
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
    return (
      <div className="flex min-h-64 flex-col items-center justify-center gap-3 rounded-xl border border-border bg-surface shadow-xs" role="status">
        <Loader />
        <span className="text-sm text-muted-foreground">{t('common.loading')}</span>
      </div>
    )
  }

  if (getUserProfileError) {
    return (
      <div className="rounded-xl border border-border bg-surface p-5 shadow-xs">
        <ApiErrorMessages error={getUserProfileError} />
      </div>
    )
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
        <Form noValidate className="rounded-xl border border-border bg-surface shadow-xs">
          <div className="border-b border-border p-5 sm:p-6">
            <FileUpload
              name="profile-image"
              accept="image/*"
              maxSizeBytes={10 * 1024 * 1024}
              forceDelete={false}
              // The avatar belongs to the account, not to a tenant, so it stays usable while acting in any tenant or in none.
              accountOwned={true}
              fileName={values.image}
              onUploaded={(res) => {
                setFieldValue('image', res.fileName)
              }}
              onClear={() => {
                setFieldValue('image', undefined)
              }}
            >
              {({ open, isUploading, isDeleting, deleteFile, selectedFileUrl }) => (
                <div className="flex flex-col items-center gap-4 text-center sm:flex-row sm:text-start">
                  <div className="size-20 shrink-0 overflow-hidden rounded-full bg-surface-2 ring-1 ring-border">
                    <Image src={selectedFileUrl || '/assets/images/default-avatar.svg'} alt={t('page.profile.altImage')} width={80} height={80} unoptimized className="h-full w-full object-cover" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-base font-semibold text-foreground">{[values.firstName, values.lastName].filter(Boolean).join(' ') || values.email}</p>
                    {(values.firstName || values.lastName) && <p className="truncate text-sm text-muted-foreground">{values.email}</p>}
                  </div>
                  <div className="flex gap-2">
                    <IconButton
                      variant="outline"
                      onClick={open}
                      aria-label={t('common.edit')}
                      title={t('common.edit')}
                      icon={<Pencil className="h-4 w-4" />}
                      isLoading={isUploading}
                      disabled={isUploading || isDeleting}
                    />
                    <IconButton
                      variant="outline-danger"
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
                      aria-label={t('common.delete')}
                      title={t('common.delete')}
                      icon={<Trash2 className="h-4 w-4" />}
                      isLoading={isDeleting}
                      disabled={isUploading || isDeleting || (!selectedFileUrl && !values.image)}
                    />
                  </div>
                </div>
              )}
            </FileUpload>
          </div>

          <div className="grid gap-5 p-5 sm:grid-cols-2 sm:p-6">
            <FormInput label={t('form.label.firstName')} name="firstName" type="text" placeholder={t('form.placeholder.firstName')} autoFocus={true} icon={<User size={16} />} />
            <FormInput label={t('form.label.lastName')} name="lastName" type="text" placeholder={t('form.placeholder.lastName')} icon={<User size={16} />} />
            <div className="sm:col-span-2">
              <FormInput label={t('form.label.email')} name="email" type="email" placeholder={t('form.placeholder.email')} icon={<Mail size={16} />} required={true} />
            </div>
          </div>

          <div className="flex flex-col-reverse gap-2 rounded-b-xl border-t border-border bg-surface-2/50 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
            <LocalizedLink href="/admin" className="btn btn-secondary">
              {t('common.cancel')}
            </LocalizedLink>
            <Button type="submit" isLoading={isUpdatingProfile || isLoadingUserProfile}>
              {t('common.submit')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
