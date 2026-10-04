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
        className="menu-surface z-50 min-w-52 [--anchor-gap:4px] focus:outline-none"
      >
        {formats.map(({ format, labelKey, icon: Icon }, index) => (
          <MenuSection key={format} className={index > 0 ? 'mt-1 border-t border-border pt-1' : undefined}>
            <MenuHeading className="flex items-center gap-2 px-2.5 py-1.5 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">
              <Icon size={14} />
              {t(labelKey)}
            </MenuHeading>
            {[false, true].map((all) => (
              <MenuItem key={String(all)}>
                <button
                  type="button"
                  className="flex w-full cursor-pointer items-center rounded-md py-1.5 ps-8 pe-3 text-sm whitespace-nowrap data-focus:bg-surface-2"
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
