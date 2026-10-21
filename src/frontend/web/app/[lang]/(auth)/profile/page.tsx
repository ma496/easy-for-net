import { UpdateProfile } from './_components/update-profile'
import { AccountShell } from '../_components/account-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: (await getServerTranslation(lang, 'page.profile.title')) || 'Profile',
  }
}

/**
 * Server-rendered profile route under the auth route group.
 * Loads localized title/description, frames the form in the single-column account layout with a back link to the admin area, and renders the update-profile form.
 */
const Profile = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.profile.title').then((text) => text || 'Profile'),
    getServerTranslation(lang, 'page.profile.description').then((text) => text || 'Update your personal information'),
  ])

  return (
    <AccountShell title={title} description={description} backHref="/">
      <UpdateProfile />
    </AccountShell>
  )
}

export default Profile
