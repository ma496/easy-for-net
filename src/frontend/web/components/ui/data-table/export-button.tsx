'use client'

import { Menu, MenuButton, MenuItem, MenuItems, MenuSection, MenuHeading } from '@headlessui/react'
import { Download, FileSpreadsheet, FileText, Loader2 } from 'lucide-react'
import { ExportFormat } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { DataTableToolbarButton } from './toolbar-button'

/** Props for DataTableExportButton, the toolbar control that exports a table's rows. */
interface DataTableExportButtonProps {
  /** Called with the chosen format and whether every filtered record (`true`) or only the current page is wanted. */
  onExport: (format: ExportFormat, all: boolean) => void
  isExporting?: boolean
  disabled?: boolean
}

const formats: { format: ExportFormat; labelKey: string; icon: typeof FileText }[] = [
  { format: 'excel', labelKey: 'table.export.excel', icon: FileSpreadsheet },
  { format: 'csv', labelKey: 'table.export.csv', icon: FileText },
]

/**
 * DataTableExportButton is an icon-only toolbar trigger opening a menu of export choices - current page or all
 * records, as Excel or CSV. The menu is portaled and anchored to the trigger, so the page layout never clips it.
 */
export function DataTableExportButton({ onExport, isExporting = false, disabled = false }: DataTableExportButtonProps) {
  const { t } = useTranslation()
  const isRTL = useAppSelector((state) => state.theme.rtlClass) === 'rtl'

  return (
    <Menu>
      <MenuButton
        as={DataTableToolbarButton}
        label={t('table.export.button')}
        icon={isExporting ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />}
        disabled={disabled || isExporting}
      />
      <MenuItems
        anchor={{ to: isRTL ? 'bottom start' : 'bottom end', gap: 6 }}
        modal={false}
        className="z-50 min-w-48 rounded-md bg-white py-1.5 text-sm text-black shadow-lg ring-1 ring-black/5 focus:outline-none dark:bg-[#1b2e4b] dark:text-white-dark"
      >
        {formats.map(({ format, labelKey, icon: Icon }, index) => (
          <MenuSection key={format} className={index > 0 ? 'mt-1 border-t border-white-light pt-1 dark:border-[#253b5c]' : undefined}>
            <MenuHeading className="flex items-center gap-2 px-4 py-1.5 text-xs font-semibold tracking-wide text-gray-500 uppercase">
              <Icon size={14} />
              {t(labelKey)}
            </MenuHeading>
            {[false, true].map((all) => (
              <MenuItem key={String(all)}>
                <button
                  type="button"
                  className="flex w-full cursor-pointer items-center py-2 ps-9 pe-4 whitespace-nowrap data-focus:bg-primary/10 data-focus:text-primary"
                  onClick={() => onExport(format, all)}
                >
                  {t(all ? 'table.export.allRecords' : 'table.export.currentPage')}
                </button>
              </MenuItem>
            ))}
          </MenuSection>
        ))}
      </MenuItems>
    </Menu>
  )
}
