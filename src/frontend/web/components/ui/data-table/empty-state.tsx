import { Inbox } from 'lucide-react'

/**
 * DataTableEmpty is the shared "no records" state of a data table, in both its table and card layouts: an icon tile above a muted message.
 */
export function DataTableEmpty({ message }: { message: React.ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
      <span className="flex size-12 items-center justify-center rounded-xl bg-surface-2 text-subtle-foreground">
        <Inbox size={22} />
      </span>
      <p className="text-sm text-muted-foreground">{message}</p>
    </div>
  )
}
