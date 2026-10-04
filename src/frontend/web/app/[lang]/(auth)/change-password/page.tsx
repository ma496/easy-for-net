import { ChangePasswordForm } from './_components/change-password-form'
import { AccountShell } from '../_components/account-shell'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.changePassword.title'),
  }
}

/**
 * Server-rendered change-password route under the auth route group.
 * Loads localized title/description, frames the form in the single-column account layout with a back link to the admin area, and renders the interactive change-password form.
 */
const ChangePassword = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.changePassword.title'),
    getServerTranslation(lang, 'page.changePassword.description'),
  ])

  return (
    <AccountShell title={title} description={description} backHref="/admin">
      <ChangePasswordForm />
    </AccountShell>
  )
}

export default ChangePassword
