import writeXlsxFile from 'write-excel-file/browser'

/** Supported export formats for tabular data downloads. */
export type ExportFormat = 'excel' | 'csv'

/**
 * Exports an array of row objects to an Excel (.xlsx) or CSV file.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function exportData(format: ExportFormat, rows: Record<string, any>[], sheetName: string, fileBaseName: string) {
  const headers = Object.keys(rows[0] ?? {})

  if (format === 'excel') {
    const data = [headers.map((value) => ({ value, fontWeight: 'bold' as const })), ...rows.map((row) => headers.map((header) => ({ value: normalizeCellValue(row[header]) })))]
    const file = writeXlsxFile(data, { sheet: sheetName.slice(0, 31) })
    void file.toFile(`${fileBaseName}.xlsx`)
  } else {
    const csv = [headers, ...rows.map((row) => headers.map((header) => normalizeCsvValue(row[header])))].map((row) => row.map(escapeCsvCell).join(',')).join('\r\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const link = document.createElement('a')
    const objectUrl = URL.createObjectURL(blob)
    link.href = objectUrl
    link.download = `${fileBaseName}.csv`
    link.click()
    URL.revokeObjectURL(objectUrl)
  }
}

const normalizeCellValue = (value: unknown): string | number | boolean | Date => {
  if (value instanceof Date || typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') return value
  return value == null ? '' : JSON.stringify(value)
}

const normalizeCsvValue = (value: unknown): string => {
  const normalized = String(normalizeCellValue(value))
  return /^[=+\-@]/.test(normalized) ? `'${normalized}` : normalized
}

const escapeCsvCell = (value: string): string => `"${value.replaceAll('"', '""')}"`
