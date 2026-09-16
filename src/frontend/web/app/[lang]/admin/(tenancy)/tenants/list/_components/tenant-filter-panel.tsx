'use client'

import { useTranslation } from '@/i18n'
import { Select } from '@/components/ui/form'
import { Search, X } from 'lucide-react'
import { Button } from '@/components/ui'
import { TenantStatus } from '@/store/api/tenancy/enums'

/**
 * Filter values accepted by the tenant list filter panel, holding the lifecycle status as the empty string when every status is shown.
 */
export interface TenantFilters {
  status: string
}

/**
 * Props for the TenantFilterPanel, including the current filter values, change handler, and search/clear callbacks.
 */
interface TenantFilterPanelProps {
  filters: TenantFilters
  onChange: (filters: TenantFilters) => void
  onSearch: () => void
  onClear: () => void
}

/**
 * Interactive client-side filter panel for the tenants list that exposes the lifecycle-status selector with search/clear actions.
 */
export const TenantFilterPanel = ({ filters, onChange, onSearch, onClear }: TenantFilterPanelProps) => {
  const { t } = useTranslation()

  const statusOptions = [
    { label: t('table.filter.allStatuses'), value: '' },
    { label: t('page.tenants.status.active'), value: TenantStatus.Active },
    { label: t('page.tenants.status.suspended'), value: TenantStatus.Suspended },
  ]

  const activeFiltersCount = [filters.status].filter(Boolean).length

  return (
    <div className="panel-2 mb-4">
      {/* Filters Row */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {/* Lifecycle Status Filter */}
        <div className="flex flex-col gap-1.5">
          <label
            htmlFor="status"
            className="text-xs font-medium text-gray-500 dark:text-gray-400"
          >
            {t('table.filter.tenantStatus')}
          </label>
          <Select
            name="status"
            id="status"
            options={statusOptions}
            value={filters.status}
            onChange={(_, value) => onChange({ ...filters, status: value })}
            placeholder={t('table.filter.allStatuses')}
            searchable={false}
            clearable={false}
            size="sm"
          />
        </div>
      </div>

      {/* Action Buttons Row */}
      <div className="mt-4 flex justify-end gap-2">
        <Button
          onClick={onSearch}
          icon={<Search className="h-4 w-4" />}
          size="sm"
        >
          {t('table.filter.search')}
        </Button>
        <Button
          variant="secondary"
          onClick={onClear}
          disabled={activeFiltersCount === 0}
          icon={<X className="h-4 w-4" />}
          size="sm"
        >
          {t('table.filter.clear')}
        </Button>
      </div>
    </div>
  )
}
