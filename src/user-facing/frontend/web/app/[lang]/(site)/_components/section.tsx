import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * One band of a public page: a `<section>` with the page's vertical rhythm and a centred, padded
 * column. `scroll-mt-20` keeps an in-page `#id` target clear of the sticky header.
 */
export const Section = ({ id, className, children }: { id?: string; className?: string; children: ReactNode }) => (
  <section id={id} className={cn('scroll-mt-20 py-20 md:py-28', className)}>
    <div className="mx-auto max-w-6xl px-4 md:px-6">{children}</div>
  </section>
)

/**
 * A section's heading block: an optional small eyebrow, the title and an optional description. An
 * `<em>` inside the title is drawn in the brand gradient. `as="h1"` makes it the page's heading.
 */
export const SectionHeading = ({
  eyebrow,
  title,
  description,
  align = 'center',
  as: Heading = 'h2',
  className,
}: {
  eyebrow?: ReactNode
  title: ReactNode
  description?: ReactNode
  align?: 'center' | 'start'
  as?: 'h1' | 'h2'
  /** Merged over the block's own classes, e.g. to drop its bottom margin. */
  className?: string
}) => (
  <div className={cn('mb-12 max-w-3xl md:mb-16', align === 'center' ? 'mx-auto text-center' : 'text-start', className)}>
    {eyebrow && <p className="mb-4 text-xs font-semibold tracking-[0.18em] text-primary uppercase">{eyebrow}</p>}
    <Heading className="text-3xl leading-[1.12] font-bold tracking-tight text-balance wrap-break-word text-foreground md:text-5xl [&_em]:bg-gradient-to-r [&_em]:from-brand [&_em]:to-brand-amber [&_em]:bg-clip-text [&_em]:text-transparent [&_em]:not-italic">
      {title}
    </Heading>
    {description && (
      <p className={cn('mt-5 text-[15px] leading-relaxed text-muted-foreground md:text-base', align === 'center' && 'mx-auto max-w-2xl')}>{description}</p>
    )}
  </div>
)

/** Bordered surface used for cards across the public pages. */
export const cardClass = 'rounded-2xl border border-border bg-surface p-6 shadow-sm'

/** Small rounded icon tile that sits on a card. */
export const iconTileClass = 'inline-flex size-11 items-center justify-center rounded-xl bg-primary/10 text-primary'

const focusRing = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

/** The loud call to action: a solid pill in the foreground colour that turns brand on hover. */
export const solidPillClass = cn(
  'inline-flex items-center gap-2 rounded-full bg-foreground px-7 py-3.5 text-sm font-semibold text-background transition-colors hover:bg-primary hover:text-primary-foreground',
  focusRing,
)

/** The quieter call to action beside a solid pill: an outlined pill. */
export const outlinePillClass = cn(
  'inline-flex items-center gap-2 rounded-full border border-foreground/30 px-7 py-3.5 text-sm font-semibold text-foreground transition-colors hover:bg-foreground/5',
  focusRing,
)
