import { lang as rootLang } from 'next/root-params'
import { FileQuestion, Home } from 'lucide-react'
import { getServerTranslation, i18nConfig } from '@/i18n'
import { LocalizedLink } from '@/components/ui'
import { StatusBackButton, StatusScreen } from './_components/status-screen'

/**
 * Server-rendered 404 view: the localized title and message, resolved on the server so the response
 * carries them, inside the status screen with two ways on - the home page first, then back.
 */
const NotFound = async () => {
  const lang = ((await rootLang()) as string | undefined) ?? i18nConfig.defaultLocale
  const [title, message, goHome] = await Promise.all([
    getServerTranslation(lang, 'error.404.title').then((text) => text || 'Not Found'),
    getServerTranslation(lang, 'error.404.message').then((text) => text || 'The requested resource was not found.'),
    getServerTranslation(lang, 'common.goHome').then((text) => text || 'Go to home page'),
  ])

  return (
    <StatusScreen
      code={404}
      icon={FileQuestion}
      title={title}
      message={message}
      action={
        <div className="mt-8 flex flex-col gap-2 sm:flex-row sm:justify-center">
          <LocalizedLink href="/" className="btn btn-primary btn-lg">
            <Home className="size-4" aria-hidden="true" />
            {goHome}
          </LocalizedLink>
          <StatusBackButton />
        </div>
      }
    />
  )
}

export default NotFound
