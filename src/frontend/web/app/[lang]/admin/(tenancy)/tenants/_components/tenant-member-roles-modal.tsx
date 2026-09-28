'use client'
import * as Yup from 'yup'
import { useTranslation } from '@/i18n'
import { useTenantMemberUpdateRolesMutation, TenantMemberListDto } from '@/store/api/tenancy'
import { useLazyRoleListQuery, RoleListDto, RoleListRequest } from '@/store/api/identity'
import { Form, Formik } from 'formik'
import { Button, Modal } from '@/components/ui'
import { FormLazyMultiSelect } from '@/components/ui/form'
import { apiErrorAlert, successToast } from '@/lib/utils'

/**
 * Builds a Yup validation schema for the member-roles form using the supplied translation function for error messages.
 */
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) => {
  return Yup.object().shape({
    roles: Yup.array()
      .of(Yup.string())
      .required(t('validation.required'))
      .min(1, t('validation.atLeastOneSelected')),
  })
}

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>

/**
 * Props for the TenantMemberRolesModal, supplying the tenant, the member whose roles are being replaced, and the open/close state.
 */
interface TenantMemberRolesModalProps {
  tenantId: string
  member: TenantMemberListDto | null
  isOpen: boolean
  onClose: () => void
}

/**
 * Interactive modal that replaces a member's role assignments inside one tenant with exactly the roles chosen
 *: the picker is scoped to that tenant's roles, and starts from the member's current assignments. Removing
 * the last tenant administrator comes back as a translated API error rather than being prevented here, since only
 * the API can decide whether another administrator is left.
 */
export const TenantMemberRolesModal = ({ tenantId, member, isOpen, onClose }: TenantMemberRolesModalProps) => {
  const { t } = useTranslation()
  const validationSchema = createValidationSchema(t)
  const [updateMemberRoles, { isLoading: isSavingRoles }] = useTenantMemberUpdateRolesMutation()

  if (!member) {
    return null
  }

  // `TenantMemberListDto.id` is the member's user account id, which is what the role-replacement route
  // addresses the membership by.
  const currentRoleIds = member.roles.map((role) => role.id)

  const onSubmit = async (data: FormValues) => {
    const result = await updateMemberRoles({
      tenantId,
      userId: member.id,
      roles: data.roles.filter((role): role is string => role !== undefined),
    })

    if (result.error) {
      apiErrorAlert(result.error)
      return
    }

    successToast.fire({
      text: t('page.tenants.members.rolesSuccess'),
    })
    onClose()
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      size="lg"
    >
      <Modal.Header>{t('page.tenants.members.rolesTitle')}</Modal.Header>
      <Formik<FormValues>
        initialValues={{ roles: currentRoleIds }}
        enableReinitialize={true}
        validationSchema={validationSchema}
        onSubmit={onSubmit}
      >
        {() => (
          <Form noValidate className="grid grid-cols-1 gap-4">
            <FormLazyMultiSelect<RoleListDto, RoleListRequest>
              name="roles"
              label={t('form.label.roles')}
              placeholder={t('form.placeholder.roles')}
              useLazyQuery={useLazyRoleListQuery}
              getLabel={(role) => role.name}
              getValue={(role) => role.id}
              selectedItemIds={currentRoleIds}
              size="sm"
              pageSize={20}
              required={true}
              // The role list is tenant-scoped, so the picker only ever offers roles of the tenant the
              // member belongs to.
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
                onClick={onClose}
                disabled={isSavingRoles}
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="submit"
                isLoading={isSavingRoles}
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
