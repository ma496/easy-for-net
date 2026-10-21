import { cn } from '@/lib/utils'

/** Props for ShowcasePreview, the padded stage a gallery example renders its live component on. */
interface ShowcasePreviewProps {
  children: React.ReactNode
  /** Lay the children out as stacked rows (full width) instead of a centred, wrapping row. */
  stack?: boolean
  className?: string
}

/**
 * ShowcasePreview is the bordered, padded well every UI gallery example renders on, so the live components sit on the same stage in light and dark mode.
 */
export const ShowcasePreview = ({ children, stack = false, className }: ShowcasePreviewProps) => (
  <div
    className={cn('flex w-full min-w-0 rounded-lg border border-border bg-background p-4 sm:p-8', stack ? 'flex-col items-stretch gap-5' : 'flex-wrap items-center justify-center gap-3', className)}
  >
    {children}
  </div>
)

/** Props for ShowcaseRow, one labelled line of examples inside a stacked ShowcasePreview. */
interface ShowcaseRowProps {
  label: string
  children: React.ReactNode
  className?: string
}

/**
 * ShowcaseRow is a labelled line of examples: an overline label beside (or, on phones, above) a wrapping row of components.
 */
export const ShowcaseRow = ({ label, children, className }: ShowcaseRowProps) => (
  <div className={cn('flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4', className)}>
    <span className="w-24 shrink-0 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{label}</span>
    <div className="flex min-w-0 flex-wrap items-center gap-2">{children}</div>
  </div>
)
