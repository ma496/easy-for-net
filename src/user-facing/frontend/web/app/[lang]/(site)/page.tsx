import { Globe, LayoutDashboard, Search, ShieldCheck } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { buildMetadata, JsonLd } from '@/lib/server/seo'
import { getIsSignupEnabled } from '@/lib/server/signup-status'
import { homeCtas, type HomeCta } from '@/lib/signup-status'

/** The home page's address, as its SEO record is keyed. */
const PATH = '/'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params
  return buildMetadata(lang, PATH)
}

/**
 * The public home page, rendered on the server: a hero with the site's promise and two ways on (sign up,
 * read more), four feature blurbs, and a closing call to action. While the site has sign-up switched off
 * the buttons offer sign-in and the About page instead (`homeCtas`), so none leads to a closed form. Its
 * title, description and sharing card come from the SEO screen, never from here.
 */
const Home = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [text, labels, isSignupEnabled] = await Promise.all([
    homeText(lang),
    Promise.all([
      getServerTranslation(lang, 'page.auth.signin.button').then((value) => value || 'Sign In'),
      getServerTranslation(lang, 'site.home.cta.closedDescription').then((value) => value || 'See what is included and how it all fits together.'),
    ]),
    getIsSignupEnabled(),
  ])
  const [badge, title, highlight, description, primaryCta, secondaryCta, featuresTitle, featuresSubtitle, seoTitle, seoText, accountsTitle, accountsText, languagesTitle, languagesText, adminTitle, adminText, ctaTitle, ctaText] = text
  const [signInLabel, ctaClosedText] = labels
  const ctas = homeCtas(isSignupEnabled)
  const ctaLabel: Record<HomeCta['label'], string> = { getStarted: primaryCta, signIn: signInLabel, learnMore: secondaryCta }

  const features = [
    { icon: Search, title: seoTitle, text: seoText },
    { icon: ShieldCheck, title: accountsTitle, text: accountsText },
    { icon: Globe, title: languagesTitle, text: languagesText },
    { icon: LayoutDashboard, title: adminTitle, text: adminText },
  ]

  return (
    <>
      <JsonLd lang={lang} path={PATH} />

      <section className="relative overflow-hidden">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,var(--color-primary)_0%,transparent_65%)] opacity-[0.12]" />
        <div className="relative mx-auto max-w-6xl px-4 pt-16 pb-20 text-center sm:px-6 sm:pt-24 sm:pb-28">
          <span className="inline-flex max-w-full items-center gap-2 rounded-full border border-border bg-surface/80 px-3 py-1 text-xs font-medium text-balance text-muted-foreground">
            <span className="size-1.5 shrink-0 rounded-full bg-primary" />
            <span className="min-w-0 wrap-break-word">{badge}</span>
          </span>
          <h1 className="mx-auto mt-6 max-w-3xl text-4xl font-semibold tracking-tight wrap-break-word text-foreground sm:text-5xl lg:text-6xl">
            {title} <span className="text-primary">{highlight}</span>
          </h1>
          <p className="mx-auto mt-5 max-w-2xl text-base leading-relaxed text-muted-foreground sm:text-lg">{description}</p>
          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
            <LocalizedLink href={ctas.heroPrimary.href} className="btn btn-primary btn-lg">
              {ctaLabel[ctas.heroPrimary.label]}
            </LocalizedLink>
            <LocalizedLink href={ctas.heroSecondary.href} className="btn btn-secondary btn-lg">
              {ctaLabel[ctas.heroSecondary.label]}
            </LocalizedLink>
          </div>
        </div>
      </section>

      <section className="border-t border-border bg-surface/50">
        <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
          <div className="max-w-2xl">
            <h2 className="text-2xl font-semibold tracking-tight text-foreground sm:text-3xl">{featuresTitle}</h2>
            <p className="mt-3 text-muted-foreground">{featuresSubtitle}</p>
          </div>
          <ul className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {features.map((feature) => (
              <li key={feature.title} className="rounded-xl border border-border bg-surface p-5 shadow-xs">
                <span className="flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <feature.icon className="size-5" aria-hidden="true" />
                </span>
                <h3 className="mt-4 text-base font-semibold wrap-break-word text-foreground">{feature.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{feature.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
        <div className="flex flex-col items-start gap-5 rounded-2xl border border-border bg-surface p-6 shadow-xs sm:flex-row sm:items-center sm:justify-between sm:p-10">
          <div className="min-w-0">
            <h2 className="text-xl font-semibold tracking-tight text-foreground sm:text-2xl">{ctaTitle}</h2>
            <p className="mt-2 text-muted-foreground">{ctas.closing.invitesSignup ? ctaText : ctaClosedText}</p>
          </div>
          <LocalizedLink href={ctas.closing.href} className="btn btn-primary btn-lg shrink-0">
            {ctaLabel[ctas.closing.label]}
          </LocalizedLink>
        </div>
      </section>
    </>
  )
}

/** The home page's own copy, each text with its English default. */
const homeText = (lang: string) =>
  Promise.all([
    getServerTranslation(lang, 'site.home.hero.badge').then((value) => value || 'Ready for your next project'),
    getServerTranslation(lang, 'site.home.hero.title').then((value) => value || 'Build something people'),
    getServerTranslation(lang, 'site.home.hero.highlight').then((value) => value || 'love to use'),
    getServerTranslation(lang, 'site.home.hero.description').then((value) => value || 'A fast, search-friendly website with visitor accounts, languages and an admin area already in place, so you can start on what makes yours different.'),
    getServerTranslation(lang, 'site.home.hero.primaryCta').then((value) => value || 'Get started'),
    getServerTranslation(lang, 'site.home.hero.secondaryCta').then((value) => value || 'Learn more'),
    getServerTranslation(lang, 'site.home.features.title').then((value) => value || 'Everything a modern site needs'),
    getServerTranslation(lang, 'site.home.features.subtitle').then((value) => value || 'The groundwork is done and tested, from the first page view to the admin screens.'),
    getServerTranslation(lang, 'site.home.features.items.seo.title').then((value) => value || 'Search friendly'),
    getServerTranslation(lang, 'site.home.features.items.seo.description').then((value) => value || 'Every page is rendered on the server, with titles, descriptions, sharing cards and a sitemap you edit from the admin area.'),
    getServerTranslation(lang, 'site.home.features.items.accounts.title').then((value) => value || 'Visitor accounts'),
    getServerTranslation(lang, 'site.home.features.items.accounts.description').then((value) => value || 'Sign-up, sign-in, email verification and password recovery, with roles and permissions behind them.'),
    getServerTranslation(lang, 'site.home.features.items.languages.title').then((value) => value || 'Many languages'),
    getServerTranslation(lang, 'site.home.features.items.languages.description').then((value) => value || 'Serve every page in the languages you choose, right-to-left included, and edit any text without a deployment.'),
    getServerTranslation(lang, 'site.home.features.items.admin.title').then((value) => value || 'Admin area'),
    getServerTranslation(lang, 'site.home.features.items.admin.description').then((value) => value || 'Manage users, roles, settings, translations and notifications from one place.'),
    getServerTranslation(lang, 'site.home.cta.title').then((value) => value || 'Ready to begin?'),
    getServerTranslation(lang, 'site.home.cta.description').then((value) => value || 'Create an account in a minute and see it for yourself.'),
  ])

export default Home
