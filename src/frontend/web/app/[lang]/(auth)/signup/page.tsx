import { SignupForm } from './_components/signup-form'
import { LanguageDropdown } from '@/components/custom'
import { Metadata } from 'next'
import { getServerTranslation } from '@/i18n'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const { lang } = await params
  return {
    title: await getServerTranslation(lang, 'page.auth.signup.title'),
  }
}

/**
 * Server-rendered boxed sign-up route under the auth route group.
 * Loads the localized title/description, frames the sign-up form with a language switcher and sign-in link, and renders the interactive form.
 */
const BoxedSignup = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [title, description] = await Promise.all([
    getServerTranslation(lang, 'page.auth.signup.title'),
    getServerTranslation(lang, 'page.auth.signup.description'),
  ])

  return (
    <div>
      <div className="relative flex min-h-screen items-center justify-center bg-cover bg-center bg-no-repeat px-6 py-10 sm:px-16 dark:bg-[#060818]">
        <div className="relative w-full max-w-217.5 rounded-md bg-[linear-gradient(45deg,#fff9f9_0%,rgba(255,255,255,0)_25%,rgba(255,255,255,0)_75%,#fff9f9_100%)] p-2 dark:bg-[linear-gradient(52.22deg,#0E1726_0%,rgba(14,23,38,0)_18.66%,rgba(14,23,38,0)_51.04%,rgba(14,23,38,0)_80.07%,#0E1726_100%)]">
          <div className="relative flex flex-col justify-center rounded-md bg-white/60 px-6 py-20 backdrop-blur-lg lg:min-h-189.5 dark:bg-black/50">
            <div className="absolute inset-e-6 top-6">
              <LanguageDropdown />
            </div>
            <div className="mx-auto w-full max-w-110">
              <div className="mb-10">
                <h1 className="text-3xl leading-snug! font-extrabold text-primary uppercase md:text-4xl">{title || 'Sign Up'}</h1>
                <p className="text-base leading-normal font-bold text-white-dark">{description || 'Enter your email and password to register'}</p>
              </div>
              <SignupForm />
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default BoxedSignup
