import { MainContainer, MainContent, ScrollToTop, Sidebar } from '@/components/layouts'

/**
 * Server-rendered layout for the admin route group, providing the authenticated admin shell (sidebar, header, content column, scroll-to-top).
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
