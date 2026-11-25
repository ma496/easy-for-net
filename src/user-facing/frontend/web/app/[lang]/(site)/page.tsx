import { ArrowRight, ArrowUpRight, Globe, LayoutDashboard, Search, ShieldCheck } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { cn } from '@/lib/utils'
import { buildMetadata, JsonLd } from '@/lib/server/seo'
import { getIsSignupEnabled } from '@/lib/server/signup-status'
import { homeCtas, type HomeCta } from '@/lib/signup-status'
import { resolveTexts } from './_components/resolve-texts'
import { outlinePillClass, Section, SectionHeading, solidPillClass } from './_components/section'

/** The home page's address, as its SEO record is keyed. */
const PATH = '/'

/** The decorative bar chart on the hero's front card, as percentages of its height. */
const bars = [18, 26, 34, 44, 56, 70, 86, 100]
const focusRing = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
const heroSolidPill = cn(
  'inline-flex items-center justify-center gap-3 rounded-full bg-foreground px-8 py-4 text-sm font-medium whitespace-nowrap text-background transition-colors hover:bg-primary hover:text-primary-foreground',
  focusRing,
)
const heroOutlinePill = cn(
  'inline-flex items-center justify-center gap-3 rounded-full border border-foreground/30 px-8 py-4 text-sm font-medium whitespace-nowrap text-foreground transition-colors hover:bg-foreground/5',
  focusRing,
)
const ctaButtonWidth = 'w-full max-w-xs justify-center sm:w-auto sm:max-w-none'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params
  return buildMetadata(lang, PATH)
}

/**
 * The public home page, rendered on the server: a hero with the site's promise and two ways on (sign up,
 * read more) beside two illustrative cards, four feature cards, and a closing call to action. While the
 * site has sign-up switched off the buttons offer sign-in and the About page instead (`homeCtas`), so none
 * leads to a closed form. Its title, description and sharing card come from the SEO screen, never from here.
 */
const Home = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [text, isSignupEnabled] = await Promise.all([homeText(lang), getIsSignupEnabled()])
  const ctas = homeCtas(isSignupEnabled)
  const ctaLabel: Record<HomeCta['label'], string> = { getStarted: text.primaryCta, signIn: text.signIn, learnMore: text.secondaryCta }
  // The closing section's second button: the About page beside sign-up, or sign-in beside the About page.
  const closingSecondary: HomeCta = ctas.closing.href === '/about' ? { href: '/signin', label: 'signIn' } : { href: '/about', label: 'learnMore' }

  const features = [
    { icon: Search, title: text.seoTitle, description: text.seoText },
    { icon: ShieldCheck, title: text.accountsTitle, description: text.accountsText },
    { icon: Globe, title: text.languagesTitle, description: text.languagesText },
    { icon: LayoutDashboard, title: text.adminTitle, description: text.adminText },
  ]

  return (
    <>
      <JsonLd lang={lang} path={PATH} />

      <section className="relative isolate overflow-hidden bg-surface text-foreground">
        {/* the warm panel behind the cards */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute end-0 bottom-0 -z-10 hidden h-[58%] w-[58%] rounded-ss-[3rem] bg-gradient-to-b from-brand-tint/70 via-brand-tint/35 to-brand-tint/10 lg:block"
        />
        <div aria-hidden="true" className="pointer-events-none absolute start-1/4 -bottom-20 -z-10 h-[420px] w-[520px] rounded-full bg-primary/10 blur-[120px]" />

        <div className="mx-auto grid max-w-7xl gap-10 px-5 pt-14 pb-16 md:px-8 md:pt-20 lg:min-h-[calc(100svh-9rem)] lg:grid-cols-12 lg:grid-rows-[auto_1fr]">
          <h1 className="animate-rise text-[2.6rem] leading-[1.02] font-medium tracking-[-0.04em] wrap-break-word sm:text-6xl md:text-7xl lg:col-span-8 lg:text-[5.4rem]">
            {text.title} <span className="text-brand">{text.highlight}</span>
          </h1>

          <div className="animate-rise flex min-w-0 flex-col items-start gap-6 [animation-delay:120ms] lg:col-span-4 lg:ps-8 lg:pt-3">
            <p className="text-sm leading-relaxed text-foreground/80 md:text-[15px]">{text.description}</p>
            <div className="flex w-full flex-wrap gap-3">
              <LocalizedLink href={ctas.heroPrimary.href} className={heroSolidPill}>
                {ctaLabel[ctas.heroPrimary.label]} <ArrowUpRight aria-hidden="true" className="size-4 shrink-0 rtl:-scale-x-100" />
              </LocalizedLink>
              <LocalizedLink href={ctas.heroSecondary.href} className={heroOutlinePill}>
                {ctaLabel[ctas.heroSecondary.label]}
              </LocalizedLink>
            </div>
          </div>

          {/* bottom row */}
          <div className="relative grid gap-8 lg:col-span-12 lg:min-h-[420px] lg:grid-cols-12">
            <div className="animate-rise flex flex-col justify-between gap-8 [animation-delay:200ms] lg:col-span-4">
              <span className="inline-flex w-fit max-w-full flex-wrap items-center gap-2 rounded-full border border-border bg-surface px-5 py-3 text-lg shadow-md">
                <span aria-hidden="true" className="size-2 shrink-0 rounded-full bg-brand" />
                <span className="min-w-0 wrap-break-word">{text.badge}</span>
              </span>
            </div>

            <div aria-hidden="true" className="animate-rise relative flex flex-col gap-4 [animation-delay:280ms] sm:block sm:min-h-[340px] lg:col-span-8">
              {/* front card */}
              <div className="w-full rounded-2xl border border-border bg-surface p-6 shadow-lg sm:absolute sm:end-0 sm:top-0 sm:max-w-[430px] lg:end-[8%]">
                <p className="text-3xl tracking-tight wrap-break-word">{text.seoTitle}</p>
                <p className="mt-1 line-clamp-2 text-xs text-subtle-foreground">{text.seoText}</p>
                <div className="mt-4 flex h-32 items-end gap-2">
                  {bars.map((height, index) => (
                    <div key={height} className="flex-1 rounded-full bg-brand-soft" style={{ height: `${height}%`, opacity: 0.12 + (index / (bars.length - 1)) * 0.88 }} />
                  ))}
                </div>
              </div>

              {/* back card */}
              <div className="w-full max-w-[290px] rounded-2xl border border-border bg-surface p-5 shadow-lg sm:absolute sm:start-[4%] sm:bottom-0 sm:w-[290px]">
                <div className="flex items-start justify-between gap-3">
                  <p className="text-2xl leading-tight tracking-tight wrap-break-word">{text.languagesTitle}</p>
                  <span className="text-primary">●</span>
                </div>
                <svg viewBox="0 0 240 70" className="mt-3 h-16 w-full" fill="none">
                  <path d="M2 66 C40 20 60 20 90 44 S150 60 190 30 S225 14 236 10" className="stroke-border" strokeWidth="3" />
                  <path d="M2 66 C40 18 60 18 90 40 S150 56 190 26 S225 12 236 8" className="stroke-primary" strokeWidth="3" />
                  <circle cx="236" cy="8" r="5" className="fill-surface stroke-primary" strokeWidth="3" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </section>

      <Section className="border-y border-border bg-surface-2">
        <SectionHeading title={text.featuresTitle} description={text.featuresSubtitle} />
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((feature) => (
            <li
              key={feature.title}
              className="group relative flex min-w-0 flex-col overflow-hidden rounded-2xl border border-border bg-surface p-6 transition hover:-translate-y-1 hover:border-primary/50 hover:shadow-lg hover:shadow-primary/10 motion-reduce:hover:translate-y-0"
            >
              <div aria-hidden="true" className="pointer-events-none absolute -end-12 -top-12 size-32 rounded-full blur-2xl transition-colors group-hover:bg-primary/15" />
              <span className="mb-5 inline-flex size-11 items-center justify-center rounded-xl bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground">
                <feature.icon aria-hidden="true" className="size-5" />
              </span>
              <h3 className="font-semibold wrap-break-word text-foreground">{feature.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{feature.description}</p>
            </li>
          ))}
        </ul>
      </Section>

      <section className="relative isolate overflow-hidden bg-primary/5 px-6 py-24 text-center text-foreground md:py-32">
        <div aria-hidden="true" className="absolute start-1/2 top-0 -z-10 h-[400px] w-[800px] -translate-x-1/2 rounded-full bg-primary/15 blur-[130px] rtl:translate-x-1/2" />
        <h2 className="mx-auto max-w-3xl text-4xl leading-tight font-bold tracking-tight text-balance wrap-break-word md:text-6xl">{text.ctaTitle}</h2>
        <p className="mx-auto mt-5 max-w-xl text-muted-foreground">{ctas.closing.invitesSignup ? text.ctaText : text.ctaClosedText}</p>
        <div className="mt-9 flex flex-col items-center justify-center gap-3 sm:flex-row sm:flex-wrap">
          <LocalizedLink href={ctas.closing.href} className={cn(solidPillClass, ctaButtonWidth)}>
            {ctaLabel[ctas.closing.label]} <ArrowRight aria-hidden="true" className="size-4 shrink-0 rtl:-scale-x-100" />
          </LocalizedLink>
          <LocalizedLink href={closingSecondary.href} className={cn(outlinePillClass, ctaButtonWidth)}>
            {ctaLabel[closingSecondary.label]}
          </LocalizedLink>
        </div>
      </section>
    </>
  )
}

/** The home page's own copy, each text with its English default. */
const homeText = (lang: string) =>
  resolveTexts({
    badge: getServerTranslation(lang, 'site.home.hero.badge').then((value) => value || 'Ready for your next project'),
    title: getServerTranslation(lang, 'site.home.hero.title').then((value) => value || 'Build something people'),
    highlight: getServerTranslation(lang, 'site.home.hero.highlight').then((value) => value || 'love to use'),
    description: getServerTranslation(lang, 'site.home.hero.description').then(
      (value) =>
        value ||
        'A fast, search-friendly website with visitor accounts, languages and an admin area already in place, so you can start on what makes yours different.',
    ),
    primaryCta: getServerTranslation(lang, 'site.home.hero.primaryCta').then((value) => value || 'Get started'),
    secondaryCta: getServerTranslation(lang, 'site.home.hero.secondaryCta').then((value) => value || 'Learn more'),
    signIn: getServerTranslation(lang, 'page.auth.signin.button').then((value) => value || 'Sign In'),
    featuresTitle: getServerTranslation(lang, 'site.home.features.title').then((value) => value || 'Everything a modern site needs'),
    featuresSubtitle: getServerTranslation(lang, 'site.home.features.subtitle').then(
      (value) => value || 'The groundwork is done and tested, from the first page view to the admin screens.',
    ),
    seoTitle: getServerTranslation(lang, 'site.home.features.items.seo.title').then((value) => value || 'Search friendly'),
    seoText: getServerTranslation(lang, 'site.home.features.items.seo.description').then(
      (value) => value || 'Every page is rendered on the server, with titles, descriptions, sharing cards and a sitemap you edit from the admin area.',
    ),
    accountsTitle: getServerTranslation(lang, 'site.home.features.items.accounts.title').then((value) => value || 'Visitor accounts'),
    accountsText: getServerTranslation(lang, 'site.home.features.items.accounts.description').then(
      (value) => value || 'Sign-up, sign-in, email verification and password recovery, with roles and permissions behind them.',
    ),
    languagesTitle: getServerTranslation(lang, 'site.home.features.items.languages.title').then((value) => value || 'Many languages'),
    languagesText: getServerTranslation(lang, 'site.home.features.items.languages.description').then(
      (value) => value || 'Serve every page in the languages you choose, right-to-left included, and edit any text without a deployment.',
    ),
    adminTitle: getServerTranslation(lang, 'site.home.features.items.admin.title').then((value) => value || 'Admin area'),
    adminText: getServerTranslation(lang, 'site.home.features.items.admin.description').then(
      (value) => value || 'Manage users, roles, settings, translations and notifications from one place.',
    ),
    ctaTitle: getServerTranslation(lang, 'site.home.cta.title').then((value) => value || 'Ready to begin?'),
    ctaText: getServerTranslation(lang, 'site.home.cta.description').then((value) => value || 'Create an account in a minute and see it for yourself.'),
    ctaClosedText: getServerTranslation(lang, 'site.home.cta.closedDescription').then((value) => value || 'See what is included and how it all fits together.'),
  })

export default Home
