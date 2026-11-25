import { Clock, Mail, MapPin, Phone } from 'lucide-react'
import { getServerTranslation } from '@/i18n'
import { cn } from '@/lib/utils'
import { buildMetadata, JsonLd } from '@/lib/server/seo'
import { cardClass, iconTileClass, Section, SectionHeading } from '../_components/section'

/** The contact page's address, as its SEO record is keyed. */
const PATH = '/contact'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params
  return buildMetadata(lang, PATH)
}

/**
 * The public contact page, rendered on the server: a short invitation and four ways to reach the site's
 * owner - email and phone as links, the address and the opening hours. Sample details for a new project
 * to replace in the translations.
 */
const Contact = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const [eyebrow, title, intro, emailLabel, emailValue, phoneLabel, phoneValue, addressLabel, addressValue, hoursLabel, hoursValue] = await Promise.all([
    getServerTranslation(lang, 'site.nav.contact').then((value) => value || 'Contact'),
    getServerTranslation(lang, 'site.contact.title').then((value) => value || 'Contact us'),
    getServerTranslation(lang, 'site.contact.intro').then((value) => value || 'Questions, ideas or feedback? We would love to hear from you and usually answer within one working day.'),
    getServerTranslation(lang, 'site.contact.email.label').then((value) => value || 'Email'),
    getServerTranslation(lang, 'site.contact.email.value').then((value) => value || 'hello@example.com'),
    getServerTranslation(lang, 'site.contact.phone.label').then((value) => value || 'Phone'),
    getServerTranslation(lang, 'site.contact.phone.value').then((value) => value || '+1 555 0100'),
    getServerTranslation(lang, 'site.contact.address.label').then((value) => value || 'Address'),
    getServerTranslation(lang, 'site.contact.address.value').then((value) => value || '100 Example Street, Springfield'),
    getServerTranslation(lang, 'site.contact.hours.label').then((value) => value || 'Opening hours'),
    getServerTranslation(lang, 'site.contact.hours.value').then((value) => value || 'Monday to Friday, 9:00 to 17:00'),
  ])

  const items = [
    { icon: Mail, label: emailLabel, value: emailValue, href: `mailto:${emailValue}`, ltr: true },
    { icon: Phone, label: phoneLabel, value: phoneValue, href: `tel:${phoneValue.replace(/[^\d+]/g, '')}`, ltr: true },
    { icon: MapPin, label: addressLabel, value: addressValue },
    { icon: Clock, label: hoursLabel, value: hoursValue },
  ]

  return (
    <>
      <JsonLd lang={lang} path={PATH} />
      <Section>
        <SectionHeading as="h1" eyebrow={eyebrow} title={title} description={intro} />

        <dl className="mx-auto grid max-w-4xl gap-4 sm:grid-cols-2">
          {items.map((item) => (
            <div key={item.label} className={cn(cardClass, 'relative min-w-0 ps-22')}>
              <dt className="text-sm text-muted-foreground">
                {/* The icon sits in the term so the list holds only terms and descriptions; it is decoration. */}
                <span aria-hidden="true" className={cn(iconTileClass, 'absolute start-6 top-6')}>
                  <item.icon className="size-5" />
                </span>
                {item.label}
              </dt>
              <dd className="mt-1 min-w-0">
                {item.href ? (
                  <a href={item.href} className="block rounded-sm font-semibold wrap-anywhere text-foreground transition-colors hover:text-primary focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none">
                    <bdi dir={item.ltr ? 'ltr' : undefined}>{item.value}</bdi>
                  </a>
                ) : (
                  <p className="font-semibold wrap-break-word text-foreground">{item.value}</p>
                )}
              </dd>
            </div>
          ))}
        </dl>
      </Section>
    </>
  )
}

export default Contact
