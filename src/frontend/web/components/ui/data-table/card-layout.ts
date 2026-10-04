import type { DataTableCardPlacement } from './column-meta'

/** Column ids the card list places by convention rather than by `meta.card`. */
export const ACTIONS_COLUMN_ID = 'actions'
export const SELECT_COLUMN_ID = 'select'

/** What the card layout needs to know about one visible column. */
export interface CardColumn {
  id: string
  placement?: DataTableCardPlacement
}

/** The visible columns of a table, sorted into the slots of a card. */
export interface CardLayout {
  select?: string
  title?: string
  subtitles: string[]
  badges: string[]
  fields: string[]
  wide: string[]
  actions?: string
}

/**
 * resolveCardLayout sorts a table's visible columns into card slots: the `select` and `actions`
 * columns by id, the rest by `meta.card`. When no column claims the title, the first unmarked data
 * column takes it, so a table with no hints still gets a heading on every card.
 */
export function resolveCardLayout(columns: CardColumn[]): CardLayout {
  const layout: CardLayout = { subtitles: [], badges: [], fields: [], wide: [] }

  for (const { id, placement } of columns) {
    if (id === SELECT_COLUMN_ID) layout.select = id
    else if (id === ACTIONS_COLUMN_ID) layout.actions = id
    else if (placement === 'hidden') continue
    else if (placement === 'title') layout.title ??= id
    else if (placement === 'subtitle') layout.subtitles.push(id)
    else if (placement === 'badge') layout.badges.push(id)
    else if (placement === 'wide') layout.wide.push(id)
    else layout.fields.push(id)
  }

  if (layout.title === undefined && layout.fields.length > 0) {
    layout.title = layout.fields.shift()
  }

  return layout
}
