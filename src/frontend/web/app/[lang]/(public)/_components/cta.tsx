import { LocalizedLink } from '@/components/ui'
import { Github } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { REPOSITORY_URL } from './links'

/**
 * Server-rendered call-to-action band of the public landing page that promotes the GitHub repository.
 * Loads localized copy and renders a heading, description, the open-source/license note, and a GitHub link, on a card with a soft accent glow.
 */
export const CTA = async ({ lang }: { lang: string }) => {
  const [title, description, button, opensource, license] = await Promise.all([
    getServerTranslation(lang, 'page.home.cta.title'),
    getServerTranslation(lang, 'page.home.cta.description'),
    getServerTranslation(lang, 'page.home.cta.button'),
    getServerTranslation(lang, 'page.home.cta.opensource'),
    getServerTranslation(lang, 'page.home.cta.license'),
  ])

  return (
    <section className="px-4 py-20 sm:px-6 sm:py-28">
      <div className="relative isolate mx-auto max-w-6xl overflow-hidden rounded-2xl border border-border bg-surface px-6 py-12 shadow-xs sm:px-12 sm:py-16">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_70%)] opacity-[0.12]" />
        <div className="flex flex-col gap-8 lg:flex-row lg:items-center lg:justify-between">
          <div className="max-w-xl">
            <h2 className="text-2xl font-semibold tracking-tight text-foreground sm:text-3xl">{title}</h2>
            <p className="mt-3 text-base leading-relaxed text-muted-foreground">{description}</p>
            <div className="mt-6 flex items-start gap-3">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-foreground ring-1 ring-border">
                <Github className="size-4" aria-hidden="true" />
              </span>
              <p className="text-sm">
                <span className="font-semibold text-foreground">{opensource}</span>
                <span className="block text-muted-foreground">{license}</span>
              </p>
            </div>
          </div>
          <LocalizedLink href={REPOSITORY_URL} target="_blank" rel="noopener noreferrer" className="btn btn-primary btn-lg shrink-0 self-start lg:self-center">
            <Github className="size-4" aria-hidden="true" />
            {button}
          </LocalizedLink>
        </div>
      </div>
    </section>
  )
}
