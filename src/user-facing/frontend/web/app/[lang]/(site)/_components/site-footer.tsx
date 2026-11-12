import { getServerTranslation } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { BrandMark } from '../../_components/brand-mark'

/**
 * The public site's footer, rendered on the server: the brand and its one-line description, the page
 * links, and the copyright line. Stacks into one column on a phone.
 */
export const SiteFooter = async ({ lang }: { lang: string }) => {
  const [tagline, home, about, contact, brandName, rights, navLabel] = await Promise.all([
    getServerTranslation(lang, 'site.footer.tagline').then((text) => text || 'A fast, search-friendly website with accounts and an admin area built in.'),
    getServerTranslation(lang, 'site.nav.home').then((text) => text || 'Home'),
    getServerTranslation(lang, 'site.nav.about').then((text) => text || 'About'),
    getServerTranslation(lang, 'site.nav.contact').then((text) => text || 'Contact'),
    getServerTranslation(lang, 'brand.name').then((text) => text || 'Easy For Net'),
    getServerTranslation(lang, 'common.allRightsReserved').then((text) => text || 'All rights reserved.'),
    getServerTranslation(lang, 'site.footer.navLabel').then((text) => text || 'Footer'),
  ])

  return (
    <footer className="border-t border-border bg-surface">
      <div className="mx-auto flex max-w-6xl flex-col gap-8 px-4 py-10 sm:px-6 md:flex-row md:items-start md:justify-between">
        <div className="max-w-sm">
          <BrandMark />
          <p className="mt-3 text-sm leading-relaxed text-muted-foreground">{tagline}</p>
        </div>
        <nav aria-label={navLabel}>
          <ul className="flex flex-wrap gap-x-6 gap-y-2 text-sm">
            {[
              { href: '/', label: home },
              { href: '/about', label: about },
              { href: '/contact', label: contact },
            ].map((link) => (
              <li key={link.href}>
                <LocalizedLink href={link.href} className="text-muted-foreground transition-colors hover:text-foreground">
                  {link.label}
                </LocalizedLink>
              </li>
            ))}
          </ul>
        </nav>
      </div>
      <div className="border-t border-border">
        <p className="mx-auto max-w-6xl px-4 py-4 text-xs text-subtle-foreground sm:px-6">
          © {new Date().getFullYear()} {brandName}. {rights}
        </p>
      </div>
    </footer>
  )
}
