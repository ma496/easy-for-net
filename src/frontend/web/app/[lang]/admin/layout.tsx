import { MainContainer, MainContent, Overlay, ScrollToTop, Setting, Sidebar } from '@/components/layouts'

/**
 * Server-rendered layout for the admin route group, providing the authenticated admin shell (sidebar, main container, settings panel, overlay, scroll-to-top).
 */
export default function DefaultLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      {/* BEGIN MAIN CONTAINER */}
      <div className="relative">
        <Overlay />
        <ScrollToTop />

        {/* BEGIN APP SETTING LAUNCHER */}
        <Setting />
        {/* END APP SETTING LAUNCHER */}

        <MainContainer>
          {/* BEGIN SIDEBAR */}
          <Sidebar />
          {/* END SIDEBAR */}
          <MainContent>{children}</MainContent>
        </MainContainer>
      </div>
    </>
  )
}
