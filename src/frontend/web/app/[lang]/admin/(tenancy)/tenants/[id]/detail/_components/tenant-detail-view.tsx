'use client'
import { Allow } from '@/allow'
import { ApiErrorMessages, Badge, Loader, LocalizedLink } from '@/components/ui'
import { useTranslation } from '@/i18n'
import { cn, isAllowed } from '@/lib/utils'
import { TenantStatus, useTenantGetQuery, useTenantMemberSeatsQuery } from '@/store/api/tenancy'
import { useAppSelector } from '@/store/hooks'
import { format } from 'date-fns'
import { ArrowRight, AtSign, Building2, Layers, Pencil, SlidersHorizontal, Users } from 'lucide-react'
import { TenantMemberTable } from '../../../_components/tenant-member-table'

/**
 * Props for the TenantDetailView, supplying the id of the tenant being shown.
 */
interface TenantDetailViewProps {
  tenantId: string
}

/**
 * One row of the overview list: a muted term and whatever stands for the value, so every field reads the same way
 * down the card whether its value is text, a badge or nothing at all.
 */
const DetailRow = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="flex items-center justify-between gap-4 py-3 sm:justify-start">
    <dt className="shrink-0 text-[13px] text-muted-foreground sm:w-40">{label}</dt>
    <dd className="min-w-0 text-end text-sm break-words text-foreground sm:text-start">{children}</dd>
  </div>
)

/**
 * How many seats the tenant has taken against what its plan allows. With no limit only the count is shown; with one,
 * a slim bar fills towards it and turns to a warning near the end and to danger once it is reached - the same point
 * at which the member table disables its add action.
 */
const SeatsCard = ({ tenantId, title }: { tenantId: string; title: string }) => {
  const { data: seats, isLoading } = useTenantMemberSeatsQuery({ tenantId })

  const limit = seats?.limit ?? null
  const used = seats?.used ?? 0
  const ratio = limit ? Math.min(used / limit, 1) : 0
  const tone = limit == null ? 'primary' : ratio >= 1 ? 'danger' : ratio >= 0.8 ? 'warning' : 'primary'

  return (
    <section className="rounded-xl border border-border bg-surface p-5 shadow-xs sm:p-6">
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-base font-semibold text-foreground">{title}</h3>
        <div className="flex size-8 items-center justify-center rounded-lg bg-surface-2 text-muted-foreground">
          <Users className="size-4" />
        </div>
      </div>

      {isLoading || !seats ? (
        <div className="mt-5 space-y-3" aria-hidden="true">
          <div className="h-8 w-24 animate-pulse rounded-md bg-surface-2" />
          <div className="h-1.5 w-full animate-pulse rounded-full bg-surface-2" />
        </div>
      ) : (
        <>
          <p className="mt-4 flex items-baseline gap-1.5 tabular-nums">
            <span className="text-3xl font-semibold tracking-tight text-foreground">{used}</span>
            <span className="text-sm text-muted-foreground">/ {limit ?? '∞'}</span>
          </p>
          {limit != null && (
            <div className="mt-4">
              <div
                className="h-1.5 w-full overflow-hidden rounded-full bg-surface-3"
                role="progressbar"
                aria-label={title}
                aria-valuemin={0}
                aria-valuemax={limit}
                aria-valuenow={used}
              >
                <div
                  className={cn(
                    'h-full rounded-full transition-[width]',
                    tone === 'danger' && 'bg-danger',
                    tone === 'warning' && 'bg-warning',
                    tone === 'primary' && 'bg-primary',
                  )}
                  style={{ width: `${Math.max(ratio * 100, used > 0 ? 2 : 0)}%` }}
                />
              </div>
              <p className="mt-2 text-xs text-muted-foreground tabular-nums">{Math.round(ratio * 100)}%</p>
            </div>
          )}
        </>
      )}
    </section>
  )
}

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
  const canViewFeatures = isAllowed(authState, [Allow.FeatureValue_View])

  if (isLoading) {
    return (
      <div className="panel flex items-center justify-center py-16">
        <Loader />
      </div>
    )
  }

  if (error) {
    return (
      <div className="panel flex items-center justify-center">
        <ApiErrorMessages error={error} />
      </div>
    )
  }

  if (!tenant) {
    return (
      <div className="panel flex flex-col items-center justify-center gap-3 py-16 text-center">
        <div className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-muted-foreground">
          <Building2 className="size-5" />
        </div>
        <p className="text-sm text-muted-foreground">{t('page.tenants.notFound')}</p>
      </div>
    )
  }

  // The system-created tenant is protected from every lifecycle change, so the way to the update form is
  // omitted for it rather than offered and then refused.
  const canOpenUpdate = canUpdate && !tenant.systemCreated

  const formatMoment = (value: string | null | undefined) => (value ? format(new Date(value), 'yyyy-MM-dd HH:mm') : '-')

  const statusBadge =
    tenant.status === TenantStatus.Active ? (
      <Badge variant="success">{t('page.tenants.status.active')}</Badge>
    ) : (
      <Badge variant="danger">{t('page.tenants.status.suspended')}</Badge>
    )

  return (
    <div className="space-y-6">
      {/* Header: who the tenant is, its state, and the ways to change it. */}
      <section className="flex flex-col gap-5 rounded-xl border border-border bg-surface p-5 shadow-xs sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div className="flex min-w-0 items-start gap-4">
          <div className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <Building2 className="size-6" />
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="min-w-0 text-xl font-semibold tracking-tight break-words text-foreground">{tenant.name}</h2>
              {statusBadge}
              {tenant.systemCreated && <Badge variant="info" type="outline">{t('page.tenants.systemCreated')}</Badge>}
            </div>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
              <span className="inline-flex min-w-0 items-center gap-1.5">
                <AtSign className="size-3.5 shrink-0 text-subtle-foreground" />
                <span className="truncate font-mono text-[13px]">{tenant.identifier}</span>
              </span>
              <span className="inline-flex min-w-0 items-center gap-1.5">
                <Layers className="size-3.5 shrink-0 text-subtle-foreground" />
                <span className="truncate">{tenant.editionName ?? '—'}</span>
              </span>
            </div>
          </div>
        </div>

        {(canOpenUpdate || canViewFeatures) && (
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            {canViewFeatures && (
              <LocalizedLink href={`/admin/tenants/${tenant.id}/features`} className="btn btn-secondary btn-sm">
                <SlidersHorizontal className="size-3.5" />
                {t('page.features.actionLabel')}
              </LocalizedLink>
            )}
            {canOpenUpdate && (
              <LocalizedLink href={`/admin/tenants/${tenant.id}/update`} className="btn btn-primary btn-sm">
                <Pencil className="size-3.5" />
                {t('common.edit')}
              </LocalizedLink>
            )}
          </div>
        )}
      </section>

      <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-3">
        <section className={cn('rounded-xl border border-border bg-surface p-5 shadow-xs sm:p-6', canViewMembers ? 'lg:col-span-2' : 'lg:col-span-3')}>
          <h3 className="text-base font-semibold text-foreground">{t('page.tenants.detail.information')}</h3>
          <dl className="mt-2 divide-y divide-border">
            <DetailRow label={t('table.columns.name')}>{tenant.name}</DetailRow>
            <DetailRow label={t('table.columns.identifier')}>
              <span className="font-mono text-[13px]">{tenant.identifier}</span>
            </DetailRow>
            <DetailRow label={t('table.columns.status')}>{statusBadge}</DetailRow>
            <DetailRow label={t('table.columns.edition')}>
              {tenant.editionName ? (
                <Badge variant="info" type="outline">{tenant.editionName}</Badge>
              ) : (
                <span className="text-subtle-foreground">&mdash;</span>
              )}
            </DetailRow>
            {/* Only the system-created tenant has a type worth naming, so the row is left out entirely rather than
                shown holding a dash for every ordinary tenant. */}
            {tenant.systemCreated && (
              <DetailRow label={t('table.columns.type')}>
                <Badge variant="info">{t('page.tenants.systemCreated')}</Badge>
              </DetailRow>
            )}
            <DetailRow label={t('table.columns.created')}>
              <span className="tabular-nums">{formatMoment(tenant.createdAt)}</span>
            </DetailRow>
            <DetailRow label={t('table.columns.updated')}>
              <span className="tabular-nums">{formatMoment(tenant.updatedAt)}</span>
            </DetailRow>
          </dl>
        </section>

        {canViewMembers && <SeatsCard tenantId={tenantId} title={t('page.tenants.detail.seats')} />}
      </div>

      {canViewMembers && (
        <section className="rounded-xl border border-border bg-surface p-4 shadow-xs sm:p-6">
          <div className="mb-4 flex items-center justify-between gap-3">
            <h3 className="text-base font-semibold text-foreground">{t('page.tenants.detail.membersTitle')}</h3>
            <LocalizedLink
              href={`/admin/tenants/${tenant.id}/members`}
              className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
            >
              {t('common.viewAll')}
              <ArrowRight className="size-3.5 rtl:-scale-x-100" />
            </LocalizedLink>
          </div>
          <TenantMemberTable tenantId={tenantId} />
        </section>
      )}
    </div>
  )
}
