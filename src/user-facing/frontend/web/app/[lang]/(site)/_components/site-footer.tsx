import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { LogoMark } from '@/components/custom'
import { getIsSignupEnabled } from '@/lib/server/signup-status'

const linkClass = 'rounded-md transition-colors hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

/** One footer link: a page of this site, locale-prefixed. */
type FooterLink = { label: string; page: string }

/**
 * The public site's footer, rendered on the server and dark in either theme: the brand and its one-line
 * description, a column of the site's pages, a column of the account pages (Sign up only while sign-up is
 * open, so no link leads to a closed form), and the copyright line. The brand takes the whole row above
 * the columns below `lg`.
 */
export const SiteFooter = async ({ lang }: { lang: string }) => {
  const [[tagline, navLabel, pagesTitle, accountTitle, home, about, contact, signIn, signUp, brandName, rights], isSignupEnabled] = await Promise.all([
    Promise.all([
      getServerTranslation(lang, 'site.footer.tagline').then((text) => text || 'A fast, search-friendly website with accounts and an admin area built in.'),
      getServerTranslation(lang, 'site.footer.navLabel').then((text) => text || 'Footer'),
      getServerTranslation(lang, 'site.footer.pages').then((text) => text || 'Pages'),
      getServerTranslation(lang, 'site.footer.account').then((text) => text || 'Account'),
      getServerTranslation(lang, 'site.nav.home').then((text) => text || 'Home'),
      getServerTranslation(lang, 'site.nav.about').then((text) => text || 'About'),
      getServerTranslation(lang, 'site.nav.contact').then((text) => text || 'Contact'),
      getServerTranslation(lang, 'page.auth.signin.button').then((text) => text || 'Sign In'),
      getServerTranslation(lang, 'page.auth.signin.signupLink').then((text) => text || 'Sign Up'),
      getServerTranslation(lang, 'brand.name').then((text) => text || 'Easy For Net'),
      getServerTranslation(lang, 'common.allRightsReserved').then((text) => text || 'All rights reserved.'),
    ]),
    getIsSignupEnabled(),
  ])

  const columns: { title: string; links: FooterLink[] }[] = [
    {
      title: pagesTitle,
      links: [
        { label: home, page: '/' },
        { label: about, page: '/about' },
        { label: contact, page: '/contact' },
      ],
    },
    {
      title: accountTitle,
      links: [{ label: signIn, page: '/signin' }, ...(isSignupEnabled ? [{ label: signUp, page: '/signup' }] : [])],
    },
  ]

  return (
    <footer className="dark border-t border-white/10 bg-background px-6 pt-16 pb-8 text-sm text-white/60">
      <nav aria-label={navLabel} className="mx-auto grid max-w-6xl grid-cols-2 gap-10 lg:grid-cols-[2fr_repeat(2,1fr)]">
        <div className="col-span-2 lg:col-span-1">
          <div className="flex items-center gap-2 text-white">
            <LogoMark />
            <span className="text-lg font-semibold">{brandName}</span>
          </div>
          <p className="mt-4 max-w-xs leading-relaxed">{tagline}</p>
        </div>
        {columns.map((column) => (
          <div key={column.title} className="min-w-0">
            <h2 className="mb-4 font-semibold text-white">{column.title}</h2>
            <ul className="space-y-2.5">
              {column.links.map((link) => (
                <li key={link.page} className="break-words">
                  <LocalizedLink href={link.page} className={linkClass}>
                    {link.label}
                  </LocalizedLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>
      <div className="mx-auto mt-12 flex max-w-6xl flex-wrap gap-x-3 gap-y-1 border-t border-white/10 pt-6 text-xs text-white/50">
        <p>
          © {new Date().getFullYear()} {brandName}. {rights}
        </p>
      </div>
    </footer>
  )
}
