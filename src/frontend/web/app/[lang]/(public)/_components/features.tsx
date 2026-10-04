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
    <section id="features" className="scroll-mt-16 border-y border-border bg-surface-2/40 py-20 sm:py-28">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="mx-auto max-w-2xl text-center">
          <p className="text-sm font-semibold text-primary">{titleBadge}</p>
          <h2 className="mt-3 text-3xl font-semibold tracking-tight text-foreground sm:text-4xl">{title}</h2>
          <p className="mt-4 text-base leading-relaxed text-muted-foreground sm:text-lg">{description}</p>
        </div>

        <dl className="mt-14 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <div key={feature.name} className="rounded-xl border border-border bg-surface p-6 shadow-xs transition-colors hover:border-primary/40">
              <dt>
                <div className="flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary ring-1 ring-primary/20">
                  <feature.icon className="size-5" aria-hidden="true" />
                </div>
                <p className="mt-5 text-base font-semibold text-foreground">{feature.name}</p>
              </dt>
              <dd className="mt-2 text-sm leading-relaxed text-muted-foreground">{feature.description}</dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  )
}

