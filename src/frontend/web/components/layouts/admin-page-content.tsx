import { cn } from '@/lib/utils'

/** Props for the AdminPageContent layout wrapper: a page header (title, description, actions) above the page body. */
interface PageContentProps {
  title?: string | React.ReactNode
  /** A line under the title saying what the page is for. */
  description?: string | React.ReactNode
  /** Controls aligned to the end of the page header, such as a primary "create" button. */
  actions?: React.ReactNode
  children: React.ReactNode
  className?: string
  /** Classes for the body container. */
  innerClassName?: React.HTMLAttributes<HTMLElement>['className']
  /** Render the body without the card surface, for pages that lay out several cards of their own. */
  plain?: boolean
}

/**
 * AdminPageContent is the standard frame of an admin page: a page header with the title, an optional description and actions, then the body on a card (or bare with `plain`).
 */
export function AdminPageContent({ title, description, actions, children, className, innerClassName, plain = false }: PageContentProps) {
  return (
    <div className={cn('w-full', className)}>
      {(title || actions) && (
        <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div className="min-w-0">
            {title && <h1 className="text-2xl font-semibold tracking-tight text-foreground">{title}</h1>}
            {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
          </div>
          {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className={cn(!plain && 'panel p-4 sm:p-6', innerClassName)}>{children}</div>
    </div>
  )
}
