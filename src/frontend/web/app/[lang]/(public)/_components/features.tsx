import { Users, Palette, Mail, Trash2, Lock, Layers } from 'lucide-react'
import { getServerTranslation } from '@/i18n'

/**
 * Server-rendered features section of the public landing page.
 * Loads the localized strings for the badge, heading, and six feature cards, then renders them in a responsive grid with icons.
 */
export const Features = async ({ lang }: { lang: string }) => {
  const [
    titleBadge,
    title,
    description,
    permissionsTitle,
    permissionsDesc,
    emailTitle,
    emailDesc,
    jobsTitle,
    jobsDesc,
    cleanupTitle,
    cleanupDesc,
    usersTitle,
    usersDesc,
    uiTitle,
    uiDesc,
  ] = await Promise.all([
    getServerTranslation(lang, 'page.home.features.titleBadge'),
    getServerTranslation(lang, 'page.home.features.title'),
    getServerTranslation(lang, 'page.home.features.description'),
    getServerTranslation(lang, 'page.home.features.items.permissions.title'),
    getServerTranslation(lang, 'page.home.features.items.permissions.description'),
    getServerTranslation(lang, 'page.home.features.items.email.title'),
    getServerTranslation(lang, 'page.home.features.items.email.description'),
    getServerTranslation(lang, 'page.home.features.items.jobs.title'),
    getServerTranslation(lang, 'page.home.features.items.jobs.description'),
    getServerTranslation(lang, 'page.home.features.items.cleanup.title'),
    getServerTranslation(lang, 'page.home.features.items.cleanup.description'),
    getServerTranslation(lang, 'page.home.features.items.users.title'),
    getServerTranslation(lang, 'page.home.features.items.users.description'),
    getServerTranslation(lang, 'page.home.features.items.ui.title'),
    getServerTranslation(lang, 'page.home.features.items.ui.description'),
  ])

  const features = [
    {
      name: permissionsTitle,
      description: permissionsDesc,
      icon: Lock,
    },
    {
      name: emailTitle,
      description: emailDesc,
      icon: Mail,
    },
    {
      name: jobsTitle,
      description: jobsDesc,
      icon: Layers,
    },
    {
      name: cleanupTitle,
      description: cleanupDesc,
      icon: Trash2,
    },
    {
      name: usersTitle,
      description: usersDesc,
      icon: Users,
    },
    {
      name: uiTitle,
      description: uiDesc,
      icon: Palette,
    },
  ]

  return (
    <div id="features" className="bg-gray-50 py-12 dark:bg-gray-900 sm:py-16 lg:py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center">
          <h2 className="text-base font-semibold tracking-wide text-primary uppercase">{titleBadge}</h2>
          <p className="mt-2 text-3xl font-extrabold leading-8 tracking-tight text-gray-900 dark:text-white sm:text-4xl">
            {title}
          </p>
          <p className="mt-4 max-w-2xl text-xl text-gray-500 dark:text-gray-400 mx-auto">
            {description}
          </p>
        </div>

        <div className="mt-10">
          <dl className="space-y-10 md:grid md:grid-cols-2 md:gap-x-8 md:gap-y-10 md:space-y-0">
            {features.map((feature) => (
              <div key={feature.name} className="relative p-6 bg-white dark:bg-gray-800 rounded-xl shadow-sm hover:shadow-md transition-shadow border border-gray-100 dark:border-gray-700">
                <dt>
                  <div className="absolute flex h-12 w-12 items-center justify-center rounded-md bg-primary/10 text-primary">
                    <feature.icon className="h-6 w-6" aria-hidden="true" />
                  </div>
                  <p className="ms-16 text-lg font-medium leading-6 text-gray-900 dark:text-white">{feature.name}</p>
                </dt>
                <dd className="mt-2 ms-16 text-base text-gray-500 dark:text-gray-400">{feature.description}</dd>
              </div>
            ))}
          </dl>
        </div>
      </div>
    </div>
  )
}

