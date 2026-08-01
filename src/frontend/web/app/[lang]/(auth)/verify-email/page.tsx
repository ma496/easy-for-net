import { getServerTranslation } from '@/i18n'
import { VerifyEmailView } from './_components/verify-email-view'
import { LanguageDropdown } from '@/components/custom/language-dropdown'
import { Metadata } from 'next'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.verifyEmail.title'),
  }
}


/**
 * Server-routed wrapper for the email-verification page that frames the VerifyEmailView component with the shared boxed auth layout.
 */
const BoxedVerifyEmail = async () => {

  return (
    <div>
      <div className="relative flex min-h-screen items-center justify-center bg-cover bg-center bg-no-repeat px-6 py-10 sm:px-16 dark:bg-[#060818]">
        <div className="relative w-full max-w-217.5 rounded-md bg-[linear-gradient(45deg,#fff9f9_0%,rgba(255,255,255,0)_25%,rgba(255,255,255,0)_75%,#fff9f9_100%)] p-2 dark:bg-[linear-gradient(52.22deg,#0E1726_0%,rgba(14,23,38,0)_18.66%,rgba(14,23,38,0)_51.04%,rgba(14,23,38,0)_80.07%,#0E1726_100%)]">
          <div className="relative flex flex-col justify-center rounded-md bg-white/60 px-6 py-20 backdrop-blur-lg lg:min-h-189.5 dark:bg-black/50">
            <div className="absolute inset-e-6 top-6">
              <LanguageDropdown />
            </div>
            <div className="mx-auto w-full max-w-110">
              <VerifyEmailView />
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default BoxedVerifyEmail
