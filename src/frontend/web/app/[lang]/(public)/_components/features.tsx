import { Bell, Building2, CalendarClock, FolderOpen, KeyRound, Languages, Layers, Lock, SlidersHorizontal } from 'lucide-react'
import { getServerTranslation } from '@/i18n'

/**
 * The capabilities the landing page advertises, in display order, each with the key under
 * `page.home.features.items` that holds its title and description, and the English shown when that key is not served.
 */
const featureItems = [
  { key: 'tenancy', icon: Building2, title: 'Multi-Tenancy', description: 'Isolated tenants with memberships, seat limits and self-service sign-up. Platform administrators manage every tenant; members switch between theirs.' },
  { key: 'permissions', icon: Lock, title: 'Roles & Permissions', description: 'Hierarchical permissions scoped to the platform or a tenant, granted through roles and enforced on every endpoint and screen.' },
  { key: 'plans', icon: Layers, title: 'Plans & Feature Flags', description: 'Editions decide which features each tenant gets and the limits it works within, such as seats and upload size, with per-tenant overrides.' },
  { key: 'sessions', icon: KeyRound, title: 'Secure Sessions', description: "Server-side sessions in Redis behind JWT or cookie sign-in. Changing a user's access ends their sessions at once." },
  { key: 'settings', icon: SlidersHorizontal, title: 'Runtime Settings', description: 'Typed settings the platform and each tenant can override without a redeploy, with secrets stored encrypted.' },
  { key: 'localization', icon: Languages, title: 'Localization & RTL', description: 'Eight languages out of the box, right-to-left layouts, and translations each tenant can edit from the admin screens.' },
  { key: 'notifications', icon: Bell, title: 'Real-Time Notifications', description: 'In-app notifications pushed over SignalR to a user, a tenant or the whole platform, scaling out through Redis.' },
  { key: 'files', icon: FolderOpen, title: 'File Management', description: "Uploads, previews and storage, with file size limits set by each tenant's plan." },
  { key: 'jobs', icon: CalendarClock, title: 'Background Jobs & Email', description: 'Hangfire for queued and recurring work, and SMTP email for verification, password resets and more.' },
] as const

/**
 * Server-rendered features section of the public landing page.
 * Loads the localized strings for the badge, heading, and feature cards, then renders them in a responsive grid with icons.
 */
export const Features = async ({ lang }: { lang: string }) => {
  const [titleBadge, title, description, features] = await Promise.all([
    getServerTranslation(lang, 'page.home.features.titleBadge').then((text) => text || 'Features'),
    getServerTranslation(lang, 'page.home.features.title').then((text) => text || 'Everything a multi-tenant product needs'),
    getServerTranslation(lang, 'page.home.features.description').then((text) => text || 'Tenancy, access control, plans, settings and the rest of the plumbing are built, tested and wired together, so your first commit can be a feature.'),
    Promise.all(
      featureItems.map(async ({ key, icon, title: englishTitle, description: englishDescription }) => ({
        key,
        icon,
        name: (await getServerTranslation(lang, `page.home.features.items.${key}.title`)) || englishTitle,
        description: (await getServerTranslation(lang, `page.home.features.items.${key}.description`)) || englishDescription,
      })),
    ),
  ])

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
            <div key={feature.key} className="rounded-xl border border-border bg-surface p-6 shadow-xs transition-colors hover:border-primary/40">
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
