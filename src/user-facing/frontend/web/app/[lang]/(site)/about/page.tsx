import { HeartHandshake, Lightbulb, Sparkles } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { buildMetadata, JsonLd } from '@/lib/server/seo'

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
  const [title, intro, missionTitle, missionText, valuesTitle, qualityTitle, qualityText, opennessTitle, opennessText, careTitle, careText, contactCta] = await Promise.all([
    getServerTranslation(lang, 'site.about.title').then((value) => value || 'About us'),
    getServerTranslation(lang, 'site.about.intro').then((value) => value || 'We are a small team that builds dependable software for the web. This page is a starting point: tell your visitors who you are and why you do what you do.'),
    getServerTranslation(lang, 'site.about.mission.title').then((value) => value || 'Our mission'),
    getServerTranslation(lang, 'site.about.mission.text').then((value) => value || 'To make good websites easier to build and easier to run, so more of every project is spent on the ideas that matter.'),
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
      <div className="mx-auto max-w-4xl px-4 py-14 sm:px-6 sm:py-20">
        <h1 className="text-3xl font-semibold tracking-tight wrap-break-word text-foreground sm:text-4xl">{title}</h1>
        <p className="mt-5 text-lg leading-relaxed text-muted-foreground">{intro}</p>

        <section className="mt-12 rounded-2xl border border-border bg-surface p-6 shadow-xs sm:p-8">
          <h2 className="text-xl font-semibold text-foreground">{missionTitle}</h2>
          <p className="mt-3 leading-relaxed text-muted-foreground">{missionText}</p>
        </section>

        <section className="mt-12">
          <h2 className="text-xl font-semibold text-foreground">{valuesTitle}</h2>
          <ul className="mt-6 grid gap-5 sm:grid-cols-3">
            {values.map((value) => (
              <li key={value.title} className="rounded-xl border border-border bg-surface p-5 shadow-xs">
                <value.icon className="size-5 text-primary" aria-hidden="true" />
                <h3 className="mt-3 font-semibold wrap-break-word text-foreground">{value.title}</h3>
                <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">{value.text}</p>
              </li>
            ))}
          </ul>
        </section>

        <div className="mt-12">
          <LocalizedLink href="/contact" className="btn btn-primary btn-lg">
            {contactCta}
          </LocalizedLink>
        </div>
      </div>
    </>
  )
}

export default About
