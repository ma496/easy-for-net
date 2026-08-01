'use client'

import { Breadcrumbs } from '../ui/breadcrumbs'
import { ContentAnimation } from './content-animation'
import { Header } from './header'
import { Portals } from '../portals'
import { Footer } from './footer'

/**
 * Primary page shell that stacks the header, animated content area (with breadcrumbs), footer, and global portals around the routed children.
 */
export const MainContent = ({ children }: { children: React.ReactNode }) => {
  return (
    <div className="main-content flex min-h-[calc(100vh-20px)] flex-col">
      {/* BEGIN TOP NAVBAR */}
      <Header />
      {/* END TOP NAVBAR */}

      {/* BEGIN CONTENT AREA */}
      <ContentAnimation>
        <Breadcrumbs className="mb-5" />
        {children}
      </ContentAnimation>
      {/* END CONTENT AREA */}

      {/* BEGIN FOOTER */}
      <Footer />
      {/* END FOOTER */}
      <Portals />
    </div>
  )
}

