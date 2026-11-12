import type { Metadata } from 'next'
import { AuthGuard } from '@/components/layouts'

/** A visitor's own account screens are never for search engines. */
export const metadata: Metadata = { robots: { index: false, follow: false } }

/**
 * Layout for the signed-in visitor's own account screens (profile, change password), behind the client
 * guard that waits for the session and sends a visitor without one to sign in.
 */
export default function AccountLayout({ children }: { children: React.ReactNode }) {
  return <AuthGuard>{children}</AuthGuard>
}
