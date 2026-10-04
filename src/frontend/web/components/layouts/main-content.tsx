'use client'

import { ContentAnimation } from './content-animation'
import { Header } from './header'
import { Portals } from '@/components'
import { Footer } from './footer'
import { TenantChangeNotice } from './tenant-change-notice'
import { useAppSelector } from '@/store/hooks'
import { cn } from '@/lib/utils'

/**
 * Primary page shell beside the sidebar: the sticky header, the page content in a centred max-width column, the footer, and global portals. From lg up its start padding follows the sidebar's width.
 */
export const MainContent = ({ children }: { children: React.ReactNode }) => {
  const collapsed = useAppSelector((state) => state.theme.sidebar)

  return (
    <div className={cn('flex min-h-screen flex-col transition-[padding] duration-200 ease-out', collapsed ? 'lg:ps-[4.5rem]' : 'lg:ps-64')}>
      <Header />
      <main className="flex-1">
        <ContentAnimation>{children}</ContentAnimation>
      </main>
      <Footer />
      <Portals />
      <TenantChangeNotice />
    </div>
  )
}
