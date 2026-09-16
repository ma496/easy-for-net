'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useLocalizedRouter } from '@/hooks'
import { useTenantOnboardMutation } from '@/store/api/tenancy'
import { useLazyGetUserInfoQuery } from '@/store/api/identity'
import { appApi } from '@/store/api/_app-api'
import { dispatchTenantChanged } from '@/store/tenant-cache'
import { useAppDispatch } from '@/store/hooks'
import { Form, Formik } from 'formik'
import { Button } from '@/components/ui'
import { FormInput } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the onboarding form using the supplied translation function for error messages.
 * The identifier rules mirror `TenantValidator` on the API - the same rules and the same duplicate comparison that
 * apply to a tenant a platform administrator creates (AC-134) - so a value that passes here is one the API accepts.
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
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Interactive client-side form through which an authenticated user holding no usable membership creates their own
 * tenant: the API makes them its administrator and switches the session into it, so the form drops every cached
 * record and reads the fresh user info before opening the tenant-scoped area. The session the response carries is
 * never stored or sent back - the caller's session cookie already covers it.
 */
export const TenantOnboardForm = () => {
  const { t } = useTranslation()
  const router = useLocalizedRouter()
  const dispatch = useAppDispatch()
  const validationSchema = createValidationSchema(t)

  const [onboardTenant, { isLoading: isOnboarding }] = useTenantOnboardMutation()
  const [getUserInfo, { isLoading: isLoadingUserInfo }] = useLazyGetUserInfoQuery()

  const isBusy = isOnboarding || isLoadingUserInfo

  const onSubmit = async (data: FormValues) => {
    const result = await onboardTenant({
      name: data.name,
      identifier: data.identifier,
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    // The session now acts in the newly created tenant, so every record cached while no tenant was selected is
    // dropped before the fresh user info is read - the order the tenant-change flow prescribes.
    dispatch(appApi.util.resetApiState())

    const userInfoResult = await getUserInfo()
    if (userInfoResult.error) {
      apiErrorAlert(userInfoResult.error)
      return
    }

    dispatchTenantChanged(dispatch, userInfoResult.data)
    successToast.fire({
      text: t('page.noTenant.createSuccess'),
    })
    router.push('/admin')
  }

  return (
    <Formik<FormValues>
      initialValues={{
        name: '',
        identifier: '',
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
          <div className="flex justify-end">
            <Button
              type="submit"
              isLoading={isBusy}
            >
              {t('page.noTenant.createButton')}
            </Button>
          </div>
        </Form>
      )}
    </Formik>
  )
}
