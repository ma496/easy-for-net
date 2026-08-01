'use client'

import { useLocalizedRouter } from '@/hooks'
import { ArrowLeft } from 'lucide-react'

/**
 * Props for the {@link BackLink} component, providing an optional display label for the link text.
 */
interface BackLinkProps {
  label?: string
}

/**
 * Renders a client-side link with a left arrow icon that navigates the user one step back in browser history using the localized router.
 */
export const BackLink = ({ label }: BackLinkProps) => {
  const router = useLocalizedRouter()

  const handleClick = (e: React.MouseEvent) => {
    e.preventDefault()
    router.back()
  }

  return (
    <a
      href="#"
      onClick={handleClick}
      className="inline-flex items-center gap-2 text-sm font-medium text-primary transition-colors hover:text-primary/80"
    >
      <ArrowLeft className="h-4 w-4" />
      {label}
    </a>
  )
}
