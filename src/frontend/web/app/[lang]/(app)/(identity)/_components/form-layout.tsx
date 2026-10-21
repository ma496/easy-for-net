import { cn } from '@/lib/utils'

/** Props for FormSection, one titled group of fields in an identity create/update form. */
interface FormSectionProps {
  /** Section heading; omit it for a block that needs no title (it still gets the divider). */
  title?: string
  /** A muted line under the heading saying what the fields are for. */
  description?: string
  /** Lay the fields out in two columns from `sm` up. */
  columns?: 1 | 2
  children: React.ReactNode
  className?: string
}

/**
 * FormSection groups related fields under an optional heading. Consecutive sections are separated
 * by a hairline, so a long form reads as a few short blocks rather than one wall of inputs.
 */
export const FormSection = ({ title, description, columns = 2, children, className }: FormSectionProps) => (
  <section className={cn('mt-6 border-t border-border pt-6 first:mt-0 first:border-t-0 first:pt-0', className)}>
    {(title || description) && (
      <div className="mb-5">
        {title && <h2 className="text-base font-semibold text-foreground">{title}</h2>}
        {description && <p className="mt-1 text-[13px] text-muted-foreground">{description}</p>}
      </div>
    )}
    <div className={cn('grid gap-5', columns === 2 && 'sm:grid-cols-2')}>{children}</div>
  </section>
)

/** Props for FormActions, the action row closing an identity form. */
interface FormActionsProps {
  children: React.ReactNode
  className?: string
}

/**
 * FormActions is the footer row of a form: a divider, then the actions aligned to the end (Cancel
 * before the primary submit). On a phone the buttons share the row equally.
 */
export const FormActions = ({ children, className }: FormActionsProps) => (
  <div className={cn('mt-6 flex justify-end gap-2 border-t border-border pt-5 *:flex-1 sm:*:flex-none', className)}>{children}</div>
)

/** Props for FormState, the centered placeholder shown while a form's record loads or fails. */
interface FormStateProps {
  children: React.ReactNode
}

/**
 * FormState centers a loader, an error banner or a not-found line in the space the form will take,
 * so the card does not collapse to a sliver while the record is fetched.
 */
export const FormState = ({ children }: FormStateProps) => <div className="flex min-h-60 items-center justify-center text-sm text-muted-foreground">{children}</div>
