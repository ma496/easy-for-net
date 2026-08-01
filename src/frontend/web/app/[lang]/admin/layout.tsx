import { MainContainer } from '@/components/layouts/main-container'
import { MainContent } from '@/components/layouts/main-content'
import { Overlay } from '@/components/layouts/overlay'
import { ScrollToTop } from '@/components/layouts/scroll-to-top'
import { Setting } from '@/components/layouts/setting'
import { Sidebar } from '@/components/layouts/sidebar'

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
