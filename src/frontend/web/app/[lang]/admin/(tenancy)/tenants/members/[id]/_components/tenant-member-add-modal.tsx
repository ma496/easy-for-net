'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useTenantMemberAddMutation } from '@/store/api/tenancy'
import {
  useLazyUserListQuery,
  UserListDto,
  UserListRequest,
  useLazyRoleListQuery,
  RoleListDto,
  RoleListRequest,
} from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { Button, Modal } from '@/components/ui'
import { FormLazyMultiSelect, FormLazySelect } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the add-member form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    userId: Yup.string().required(t('validation.required')),
    roles: Yup.array()
      .of(Yup.string())
      .required(t('validation.required'))
      .min(1, t('validation.atLeastOneSelected')),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Props for the TenantMemberAddModal, supplying the tenant the member is added to and the open/close state.
 */
interface TenantMemberAddModalProps {
  tenantId: string
  isOpen: boolean
  onClose: () => void
}

/**
 * Interactive modal for adding an existing user account to a tenant with exactly the roles chosen: the user picker
 * searches the global account list, while the role picker offers only the roles of the tenant being added to
 * (AC-038). A duplicate membership or an unknown account comes back as a translated API error.
 */
export const TenantMemberAddModal = ({ tenantId, isOpen, onClose }: TenantMemberAddModalProps) => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [addMember, { isLoading: isAddingMember }] = useTenantMemberAddMutation()

  const onSubmit = async (data: FormValues, { resetForm }: { resetForm: () => void }) => {
    const result = await addMember({
      tenantId,
      userId: data.userId,
      roles: data.roles.filter((role): role is string => role !== undefined),
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.tenants.members.addSuccess'),
    })
    resetForm()
    onClose()
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      size="lg"
    >
      <Modal.Header>{t('page.tenants.members.addTitle')}</Modal.Header>
      <Formik<FormValues>
        initialValues={{
          userId: '',
          roles: [],
        }}
        validationSchema={validationSchema}
        onSubmit={onSubmit}
      >
        {({ resetForm }) => (
          <Form noValidate className="grid grid-cols-1 gap-4">
            <FormLazySelect<UserListDto, UserListRequest>
              name="userId"
              label={t('form.label.user')}
              placeholder={t('form.placeholder.user')}
              useLazyQuery={useLazyUserListQuery}
              getLabel={(user) => user.username}
              getValue={(user) => user.id}
              size="sm"
              pageSize={20}
              required={true}
            />
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
              // The role list is tenant-scoped, so the picker only ever offers roles of the tenant
              // the member is being added to (AC-038).
              generateRequest={(search, page, pageSize) => ({
                page,
                pageSize,
                search: search || undefined,
                all: true,
                tenantId,
              })}
            />
            <div className="flex justify-end gap-4">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  resetForm()
                  onClose()
                }}
                disabled={isAddingMember}
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="submit"
                isLoading={isAddingMember}
              >
                {t('common.submit')}
              </Button>
            </div>
          </Form>
        )}
      </Formik>
    </Modal>
  )
}
