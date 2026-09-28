'use client'

import { Filter } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { DataTableToolbarButton } from './toolbar-button'

/**
 * Props for the DataTableFilterButton, describing its open state, toggle handler, and the number of active filters to display in a badge.
 */
interface DataTableFilterButtonProps {
  isOpen: boolean
  onToggle: () => void
  activeFiltersCount?: number
}

/**
 * Icon-only toolbar button that opens or closes a table's filter panel, with its label in a tooltip and a badge counting the active filters.
 */
export const DataTableFilterButton = ({ isOpen, onToggle, activeFiltersCount = 0 }: DataTableFilterButtonProps) => {
  const { t } = useTranslation()

  return (
    <DataTableToolbarButton label={t('table.filter.button')} icon={<Filter size={16} />} active={isOpen} aria-expanded={isOpen} onClick={onToggle}>
    {activeFiltersCount > 0 && (
      <span className="absolute -top-1.5 -end-1.5 flex h-4.5 min-w-4.5 items-center justify-center rounded-full bg-primary px-1 text-[10px] font-semibold text-white ring-2 ring-white dark:ring-[#0e1726]">
        {activeFiltersCount}
      </span>
    )}
    </DataTableToolbarButton>
  )
}
