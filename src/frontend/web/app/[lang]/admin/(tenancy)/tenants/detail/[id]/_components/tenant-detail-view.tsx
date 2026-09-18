'use client'
import { Allow } from '@/allow'
import { ApiErrorMessages, Badge, Loader, LocalizedLink } from '@/components/ui'
import { useTranslation } from '@/i18n'
import { isAllowed } from '@/lib/utils'
import { TenantStatus, useTenantGetQuery } from '@/store/api/tenancy'
import { useAppSelector } from '@/store/hooks'
import { format } from 'date-fns'
import { Pencil } from 'lucide-react'
import { TenantMemberTable } from '../../../_components/tenant-member-table'

/**
 * Props for the TenantDetailView, supplying the id of the tenant being shown.
 */
interface TenantDetailViewProps {
  tenantId: string
}

/**
 * One row of the detail card: a label and whatever stands for the value, so every field reads the same way down
 * the card whether its value is text, a badge or nothing at all.
 */
const DetailRow = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="flex flex-col gap-1 border-b border-white-light py-3 last:border-b-0 sm:flex-row sm:items-center sm:gap-4 dark:border-white-light/10">
    <span className="w-48 shrink-0 text-sm font-semibold text-white-dark">{label}</span>
    <span className="text-sm break-words">{children}</span>
  </div>
)

/**
 * Interactive client-side view of a single tenant: what it is - its display name, the identifier it is addressed
 * by, its lifecycle state and the trail of who created and last changed it - followed by the people in it, which
 * is where a member is added to the tenant from.
 *
 * Which tenants can be read at all is settled by the API: a platform user reads any of them, and everybody else
 * only the tenants they hold an active membership in, so a tenant that is none of the caller's business reads as
 * missing here exactly as a deleted one does.
 */
export const TenantDetailView = ({ tenantId }: TenantDetailViewProps) => {
  const { t } = useTranslation()
  const authState = useAppSelector((state) => state.auth)
  const { data: tenant, isLoading, error } = useTenantGetQuery({ id: tenantId })

  const canUpdate = isAllowed(authState, [Allow.Tenant_Update])
  const canViewMembers = isAllowed(authState, [Allow.TenantMember_View])

  if (isLoading) {
    return (
      <div className="flex items-center justify-center">
        <Loader />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  if (!tenant) {
    return <div className="flex items-center justify-center">{t('page.tenants.notFound')}</div>
  }

  // The system-created tenant is protected from every lifecycle change, so the way to the update form is
  // omitted for it rather than offered and then refused.
  const canOpenUpdate = canUpdate && !tenant.systemCreated

  const formatMoment = (value: string | null | undefined) => (value ? format(new Date(value), 'yyyy-MM-dd HH:mm') : '-')

  return (
    <div className="space-y-6">
      <div className="panel">
        <div className="mb-4 flex items-center justify-between gap-4">
          <h2 className="text-lg font-semibold dark:text-white-light">{t('page.tenants.detail.information')}</h2>
          {canOpenUpdate && (
            <LocalizedLink href={`/admin/tenants/update/${tenant.id}`} className="btn btn-secondary btn-sm">
              <Pencil className="me-1 h-3 w-3" />
              {t('common.edit')}
            </LocalizedLink>
          )}
        </div>

        <div className="flex flex-col">
          <DetailRow label={t('table.columns.name')}>{tenant.name}</DetailRow>
          <DetailRow label={t('table.columns.identifier')}>{tenant.identifier}</DetailRow>
          <DetailRow label={t('table.columns.status')}>
            {tenant.status === TenantStatus.Active ? (
              <Badge variant="success">{t('page.tenants.status.active')}</Badge>
            ) : (
              <Badge variant="danger">{t('page.tenants.status.suspended')}</Badge>
            )}
          </DetailRow>
          {/* Only the system-created tenant has a type worth naming, so the row is left out entirely rather than
              shown holding a dash for every ordinary tenant. */}
          {tenant.systemCreated && (
            <DetailRow label={t('table.columns.type')}>
              <Badge variant="info">{t('page.tenants.systemCreated')}</Badge>
            </DetailRow>
          )}
          <DetailRow label={t('table.columns.created')}>{formatMoment(tenant.createdAt)}</DetailRow>
          <DetailRow label={t('table.columns.updated')}>{formatMoment(tenant.updatedAt)}</DetailRow>
        </div>
      </div>

      {canViewMembers && (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold dark:text-white-light">{t('page.tenants.detail.membersTitle')}</h2>
          <TenantMemberTable tenantId={tenantId} />
        </div>
      )}
    </div>
  )
}
