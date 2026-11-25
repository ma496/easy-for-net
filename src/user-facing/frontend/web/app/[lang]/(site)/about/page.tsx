import { ArrowRight, HeartHandshake, Lightbulb, Sparkles, Target } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { cn } from '@/lib/utils'
import { buildMetadata, JsonLd } from '@/lib/server/seo'
import { cardClass, iconTileClass, Section, SectionHeading, solidPillClass } from '../_components/section'

/** The about page's address, as its SEO record is keyed. */
const PATH = '/about'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params
  return buildMetadata(lang, PATH)
}

/**
 * The public about page, rendered on the server: who the site is for, its mission, three values and a
 * link to the contact page. Sample copy for a new project to replace.
 */
const About = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [eyebrow, title, intro, missionTitle, missionText, valuesTitle, qualityTitle, qualityText, opennessTitle, opennessText, careTitle, careText, contactCta] =
    await Promise.all([
      getServerTranslation(lang, 'site.nav.about').then((value) => value || 'About'),
      getServerTranslation(lang, 'site.about.title').then((value) => value || 'About us'),
      getServerTranslation(lang, 'site.about.intro').then(
        (value) =>
          value ||
          'We are a small team that builds dependable software for the web. This page is a starting point: tell your visitors who you are and why you do what you do.',
      ),
      getServerTranslation(lang, 'site.about.mission.title').then((value) => value || 'Our mission'),
      getServerTranslation(lang, 'site.about.mission.text').then(
        (value) => value || 'To make good websites easier to build and easier to run, so more of every project is spent on the ideas that matter.',
      ),
      getServerTranslation(lang, 'site.about.values.title').then((value) => value || 'What we value'),
      getServerTranslation(lang, 'site.about.values.items.quality.title').then((value) => value || 'Quality'),
      getServerTranslation(lang, 'site.about.values.items.quality.description').then((value) => value || 'We would rather ship one thing that works than three that almost do.'),
      getServerTranslation(lang, 'site.about.values.items.openness.title').then((value) => value || 'Openness'),
      getServerTranslation(lang, 'site.about.values.items.openness.description').then((value) => value || 'We explain our choices plainly and share what we learn.'),
      getServerTranslation(lang, 'site.about.values.items.care.title').then((value) => value || 'Care'),
      getServerTranslation(lang, 'site.about.values.items.care.description').then((value) => value || 'Every visitor deserves a page that is fast, clear and accessible.'),
      getServerTranslation(lang, 'site.about.contactCta').then((value) => value || 'Get in touch'),
    ])

  const values = [
    { icon: Sparkles, title: qualityTitle, text: qualityText },
    { icon: Lightbulb, title: opennessTitle, text: opennessText },
    { icon: HeartHandshake, title: careTitle, text: careText },
  ]

  return (
    <>
      <JsonLd lang={lang} path={PATH} />

      <Section className="border-b border-border bg-surface">
        <SectionHeading as="h1" eyebrow={eyebrow} title={title} description={intro} className="mb-0 md:mb-0" />
      </Section>

      <Section>
        <div className={cn(cardClass, 'mx-auto max-w-3xl md:p-10')}>
          <span className={cn(iconTileClass, 'mb-5')}>
            <Target aria-hidden="true" className="size-5" />
          </span>
          <h2 className="text-2xl font-semibold tracking-tight wrap-break-word text-foreground">{missionTitle}</h2>
          <p className="mt-3 leading-relaxed text-muted-foreground">{missionText}</p>
        </div>
      </Section>

      <Section className="border-y border-border bg-surface-2">
        <SectionHeading title={valuesTitle} />
        <ul className="grid gap-4 sm:grid-cols-3">
          {values.map((value) => (
            <li key={value.title} className={cn(cardClass, 'min-w-0')}>
              <span className={cn(iconTileClass, 'mb-5')}>
                <value.icon aria-hidden="true" className="size-5" />
              </span>
              <h3 className="font-semibold wrap-break-word text-foreground">{value.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{value.text}</p>
            </li>
          ))}
        </ul>
        <div className="mt-12 text-center">
          <LocalizedLink href="/contact" className={solidPillClass}>
            {contactCta} <ArrowRight aria-hidden="true" className="size-4 shrink-0 rtl:-scale-x-100" />
          </LocalizedLink>
        </div>
      </Section>
    </>
  )
}

export default About
