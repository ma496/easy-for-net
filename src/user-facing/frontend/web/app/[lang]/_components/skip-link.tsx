'use client'

import { useTranslation } from '@/i18n'

/** The id of the `<main>` element the skip link jumps to; each shell puts it on its own `<main>`. */
export const MAIN_CONTENT_ID = 'content'

/**
 * "Skip to content": the first focusable element of a shell, hidden until it is focused, which moves
 * keyboard and screen-reader users past the header and navigation to the page's `<main>`.
 */
export const SkipLink = () => {
  const { t } = useTranslation()

  return (
    <a href={`#${MAIN_CONTENT_ID}`} className="btn btn-primary btn-sm sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50">
      {t('site.skipToContent') || 'Skip to content'}
    </a>
  )
}
