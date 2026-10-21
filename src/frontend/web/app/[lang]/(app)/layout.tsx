import { MainContainer, MainContent, ScrollToTop, Sidebar } from '@/components/layouts'

/**
 * Server-rendered layout for the (app) route group, providing the authenticated app shell (sidebar, header, content column, scroll-to-top).
 */
export default function DefaultLayout({ children }: { children: React.ReactNode }) {
  return (
    <MainContainer>
      <ScrollToTop />
      <Sidebar />
      <MainContent>{children}</MainContent>
    </MainContainer>
  )
}
