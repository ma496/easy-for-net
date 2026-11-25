import { SiteFooter } from './_components/site-footer'
import { SiteHeader } from './_components/site-header'
import { MAIN_CONTENT_ID, SkipLink } from '../_components/skip-link'

/**
 * Layout of the public site: a skip link, the header, the page, and the footer, in the site's brand orange
 * (`site-theme`). Nothing here waits on the visitor's
 * session - every page is server-rendered with its content in the first HTML, and the header fills in
 * the account menu once the account info answers.
 */
export default async function SiteLayout({ children, params }: { children: React.ReactNode; params: Promise<{ lang: string }> }) {
  const { lang } = await params

  return (
    <div className="site-theme relative flex min-h-screen flex-col overflow-x-clip bg-background text-foreground">
      <SkipLink />
      <SiteHeader />
      <main id={MAIN_CONTENT_ID} tabIndex={-1} className="flex-1 outline-none">
        {children}
      </main>
      <SiteFooter lang={lang} />
    </div>
  )
}
