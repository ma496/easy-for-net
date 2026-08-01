'use client'

import { useTranslation } from '@/i18n'
import { Select } from '@/components/ui/form'
import { Search, X } from 'lucide-react'
import { Button, Loading, ApiErrorMessages } from '@/components/ui'
import { useNotificationGetGroupsQuery } from '@/store/api/notifications'

/**
 * Filter values accepted by the notification list filter panel, representing read-status and group selection as strings.
 */
export interface NotificationFilters {
  isRead: string
  group: string
}

/**
 * Props for the NotificationFilterPanel, including the current filter values, change handler, and search/clear callbacks.
 */
interface NotificationFilterPanelProps {
  filters: NotificationFilters
  onChange: (filters: NotificationFilters) => void
  onSearch: () => void
  onClear: () => void
}

/**
 * Interactive client-side filter panel for the notifications list that exposes read-status and group selectors with search/clear actions.
 */
export const NotificationFilterPanel = ({ filters, onChange, onSearch, onClear }: NotificationFilterPanelProps) => {
  const { t } = useTranslation()
  const { data: groupsData, isLoading: isGroupsLoading, error: groupsError } = useNotificationGetGroupsQuery({})

  const isReadOptions = [
    { label: t('notifications.all') || 'All', value: '' },
    { label: t('notifications.read') || 'Read', value: 'true' },
    { label: t('notifications.unread') || 'Unread', value: 'false' },
  ]

  const groupOptions = [
    { label: t('notifications.all') || 'All', value: '' },
    ...(groupsData?.groups ?? []).map(group => ({
      label: t(`notifications.groups.${group}`, { defaultValue: group }),
      value: group
    }))
  ]

  const activeFiltersCount = [filters.isRead, filters.group].filter(Boolean).length

  if (isGroupsLoading) {
    return (
      <div className="flex justify-center items-center">
        <Loading />
      </div>
    )
  }

  if (groupsError) {
    return (
      <div className="flex justify-center items-center">
        <ApiErrorMessages error={groupsError} />
      </div>
    )
  }

  return (
    <div className="mb-4 panel-2">
      {/* Filters Row */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {/* IsRead Status Filter */}
        <div className="flex flex-col gap-1.5">
          <label
            htmlFor="isRead"
            className="text-xs font-medium text-gray-500 dark:text-gray-400">
            {t('table.columns.status')}
          </label>
          <Select
            name="isRead"
            id="isRead"
            options={isReadOptions}
            value={filters.isRead}
            onChange={(_, value) => onChange({ ...filters, isRead: value })}
            placeholder={t('notifications.all')}
            searchable={false}
            clearable={false}
            size="sm"
          />
        </div>

        {/* Group Filter */}
        <div className="flex flex-col gap-1.5">
          <label
            htmlFor="group"
            className="text-xs font-medium text-gray-500 dark:text-gray-400">
            {t('table.columns.group')}
          </label>
          <Select
            name="group"
            id="group"
            options={groupOptions}
            value={filters.group}
            onChange={(_, value) => onChange({ ...filters, group: value })}
            placeholder={t('notifications.filterByGroup') || 'Filter by group'}
            searchable={false}
            clearable={true}
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
          {t('table.filter.search') || 'Search'}
        </Button>
        <Button
          variant="secondary"
          onClick={onClear}
          disabled={activeFiltersCount === 0}
          icon={<X className="h-4 w-4" />}
          size="sm"
        >
          {t('table.filter.clear') || 'Clear'}
        </Button>
      </div>
    </div>
  )
}
