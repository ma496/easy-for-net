import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { Github } from 'lucide-react'
import { BrandMark } from '../../_components/brand-mark'
import { REPOSITORY_URL } from './links'

/**
 * Server-rendered site footer used on the public landing page.
 * Displays the brand mark, the current-year copyright line, and a link to the source repository.
 */
export const Footer = async ({ lang }: { lang: string }) => {
  const [brandName, allRightsReserved, viewSource] = await Promise.all([
    getServerTranslation(lang, 'brand.name'),
    getServerTranslation(lang, 'common.allRightsReserved'),
    getServerTranslation(lang, 'page.home.hero.viewSource'),
  ])

  return (
    <footer className="border-t border-border">
      <div className="mx-auto flex max-w-6xl flex-col items-center gap-4 px-4 py-8 sm:flex-row sm:justify-between sm:px-6">
        <div className="flex flex-col items-center gap-3 sm:flex-row sm:gap-4">
          <BrandMark />
          <p className="text-center text-sm text-subtle-foreground">
            © {new Date().getFullYear()}. {brandName}. {allRightsReserved}
          </p>
        </div>
        <LocalizedLink
          href={REPOSITORY_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-2 rounded-md text-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <Github className="size-4" aria-hidden="true" />
          {viewSource}
        </LocalizedLink>
      </div>
    </footer>
  )
}
