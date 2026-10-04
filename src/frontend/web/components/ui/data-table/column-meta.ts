import type { RowData } from '@tanstack/react-table'

/**
 * Where a column's cell goes in the card list that small screens get instead of the table.
 * - `title`: the card's heading (defaults to the first data column when no column claims it).
 * - `subtitle`: a muted line under the heading.
 * - `badge`: beside the heading, for a status pill.
 * - `wide`: a labelled field spanning the whole card, for long text.
 * - `hidden`: left off the card.
 * Unmarked columns become labelled fields in a two-column grid.
 */
export type DataTableCardPlacement = 'title' | 'subtitle' | 'badge' | 'wide' | 'hidden'

declare module '@tanstack/react-table' {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData extends RowData, TValue> {
    card?: DataTableCardPlacement
  }
}
