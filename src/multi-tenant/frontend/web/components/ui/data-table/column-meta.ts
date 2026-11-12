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

/** What a data-table column may declare under `meta`, registered on the table's features as its column-meta slot. */
export interface DataTableColumnMeta {
  card?: DataTableCardPlacement
}
