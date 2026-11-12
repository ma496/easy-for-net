import type { Metadata } from 'next'
import { AuthGuard, MainContainer, MainContent, ScrollToTop, Sidebar } from '@/components/layouts'
import { SkipLink } from '../_components/skip-link'

/** The admin area is never for search engines. */
export const metadata: Metadata = { robots: { index: false, follow: false } }

/**
 * Server-rendered layout for the admin area, providing the signed-in app shell (a skip link past the sidebar and header to the page's `<main>`, sidebar, header, content
 * column, scroll-to-top) behind the client guard that waits for the session and checks the screen's permissions.
 */
export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard>
      <MainContainer>
        <SkipLink />
        <ScrollToTop />
        <Sidebar />
        <MainContent>{children}</MainContent>
      </MainContainer>
    </AuthGuard>
  )
}
