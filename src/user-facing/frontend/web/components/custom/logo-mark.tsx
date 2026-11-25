import { cn } from '@/lib/utils'

/**
 * Props for the {@link LogoMark} component.
 */
interface LogoMarkProps {
  /** Extra classes merged over the default `size-8 shrink-0`, to resize the mark. */
  className?: string
}

/**
 * The brand mark: an orange tile with a stepped "E". It mirrors `app/icon.svg` (the favicon)
 * and `app/apple-icon.tsx`, and is brand art, so its colours are fixed and do not follow the theme.
 * Decorative: the link or text beside it carries the name.
 */
export const LogoMark = ({ className }: LogoMarkProps) => (
  <svg viewBox="0 0 32 32" aria-hidden="true" focusable="false" className={cn('size-8 shrink-0', className)}>
    <rect width="32" height="32" rx="8" fill="#f97316" />
    <g fill="#fff">
      <rect x="9" y="8.5" width="14" height="3.5" rx="1.75" />
      <rect x="9" y="14.25" width="9.5" height="3.5" rx="1.75" />
      <rect x="9" y="20" width="14" height="3.5" rx="1.75" />
    </g>
  </svg>
)
