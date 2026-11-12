'use client'

import { useTranslation } from '@/i18n'
import { Select } from '@/components/ui/form'
import { useRoleListQuery } from '@/store/api/identity'
import { Search, X } from 'lucide-react'
import { Button, Loader, ApiErrorMessages } from '@/components/ui'

/**
 * Filter values accepted by the user list filter panel, representing active status and role selection as strings.
 */
export interface UserFilters {
  isActive: string
  roleId: string
}

/**
 * Props for the UserFilterPanel, including the current filter values, change handler, and search/clear callbacks.
 */
interface UserFilterPanelProps {
  filters: UserFilters
  onChange: (filters: UserFilters) => void
  onSearch: () => void
  onClear: () => void
}

/**
 * Interactive client-side filter panel for the users list that exposes active-status and role selectors with search/clear actions.
 */
export const UserFilterPanel = ({ filters, onChange, onSearch, onClear }: UserFilterPanelProps) => {
  const { t } = useTranslation()

  const { data: rolesData, isLoading: isRolesLoading, error: rolesError } = useRoleListQuery({ all: true })

  const activeFilterOptions = [
    { label: t('table.filter.all') || 'All', value: '' },
    { label: t('table.filter.active') || 'Active', value: 'true' },
    { label: t('table.filter.inactive') || 'Inactive', value: 'false' },
  ]

  const roleOptions = [
    { label: t('table.filter.allRoles') || 'All Roles', value: '' },
    ...(rolesData?.items.map((role) => ({
      label: role.name,
      value: role.id,
    })) || []),
  ]

  const activeFiltersCount = [filters.isActive, filters.roleId].filter(Boolean).length

  if (isRolesLoading) {
    return (
      <div className="mb-4 panel-2 flex min-h-24 items-center justify-center">
        <Loader />
      </div>
    )
  }

  if (rolesError) {
    return (
      <div className="mb-4">
        <ApiErrorMessages error={rolesError} />
      </div>
    )
  }

  return (
    <div className="mb-4 panel-2">
      {/* Filters and actions: stacked on a phone, two columns from sm, one row with the actions at the end from lg */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] lg:items-end">
        {/* Active Status Filter */}
        <div className="min-w-0">
          <label
            htmlFor="isActive"
            className="mb-1.5 block text-xs font-medium text-muted-foreground">
            {t('table.filter.activeStatus') || 'Active Status'}
          </label>
          <Select
            name="isActive"
            id="isActive"
            options={activeFilterOptions}
            value={filters.isActive}
            onChange={(_, value) => onChange({ ...filters, isActive: value })}
            placeholder={t('table.filter.all') || 'All'}
            searchable={false}
            clearable={false}
            size="sm"
          />
        </div>

        {/* Role Filter */}
        <div className="min-w-0">
          <label
            htmlFor="roleId"
            className="mb-1.5 block text-xs font-medium text-muted-foreground">
            {t('table.filter.role') || 'Role'}
          </label>
          <Select
            name="roleId"
            id="roleId"
            options={roleOptions}
            value={filters.roleId}
            onChange={(_, value) => onChange({ ...filters, roleId: value })}
            placeholder={t('table.filter.selectRole') || 'Select Role'}
            searchable={true}
            clearable={true}
            size="sm"
          />
        </div>

        {/* Action Buttons */}
        <div className="flex justify-end gap-2 sm:col-span-2 lg:col-span-1">
          <Button
            variant="ghost"
            onClick={onClear}
            disabled={activeFiltersCount === 0}
            icon={<X className="h-4 w-4" />}
            size="sm"
          >
            {t('table.filter.clear') || 'Clear'}
          </Button>
          <Button
            onClick={onSearch}
            icon={<Search className="h-4 w-4" />}
            size="sm"
          >
            {t('table.filter.search') || 'Search'}
          </Button>
        </div>
      </div>
    </div>
  )
}
