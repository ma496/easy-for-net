---
name: frontend-page
description: Add a route to the Next.js app under src/frontend/web/app/[lang] — a public (site) page, a signed-in account screen or an admin screen; server page component, _components client component, locale-prefixed routing, the access layers (proxy.ts sign-in check for /admin and /account, auth-urls.ts permissions with Admin.Access on every admin screen, the client AuthGuard), i18n keys, nav-items and searchable-items registration. Use for any new screen that is not a full CRUD set.
---

# Adding a page

## Route layout

Every route is locale-prefixed: `app/[lang]/…`. The site has three areas, and a new screen belongs to
exactly one of them:

- `(site)` — the **public site**, at `/` (`(site)/page.tsx` is the home page, `(site)/about` is
  `/about`). Server-rendered with its content in the first HTML, open to every visitor and crawler,
  framed by `SiteHeader`/`SiteFooter` in `(site)/layout.tsx`, and never behind the client guard. A
  public page also carries its SEO metadata — see the `seo` skill, which is the guide for these.
- `(account)` — the anonymous account screens (`signin`, `signup`, `forget-password`,
  `reset-password`, `verify-email`, `unauthorized`, framed by `_components/auth-shell.tsx`) and the
  signed-in visitor's own screens under `(account)/account/` (`/account/profile`,
  `/account/change-password`, framed by `_components/account-shell.tsx`). `(account)/account/layout.tsx`
  wraps them in `AuthGuard` and marks them `noindex`.
- `admin` — the **admin area**, a real URL segment (`/admin`, `/admin/users`, …): the signed-in shell
  (sidebar, header) in `admin/layout.tsx`, behind `AuthGuard`, `noindex`. The admin dashboard is
  `admin/page.tsx`. Nested route groups such as `admin/(identity)` group a feature's screens without
  adding a segment (`admin/(identity)/users` is `/admin/users`).

Build every admin and account address with `adminPath('/users')` and `accountPath('/profile')` from
`lib/paths.ts` (with `isAdminPath`/`isAccountPath` to test one) rather than writing `/admin/…` by hand.

A screen is a folder with `page.tsx` plus a sibling `_components/` folder holding the interactive
client parts. Dynamic segments are folders, and a screen about one record puts the id first:
`[id]/update/page.tsx` (`/admin/seo/{id}/update`). A client component shared by several screens of one
area sits in that area's own `_components/` (`admin/_components/form-layout.tsx`,
`admin/seo/_components/seo-page-editors.tsx`).

## The page component (server)

`page.tsx` is a **server** component. An admin screen awaits `params`, resolves the title with
`getServerTranslation`, and renders the client component inside `PageContent`:

```tsx
import { getServerTranslation } from '@/i18n'
import { PageContent } from '@/components/layouts'
import { SeoPageUpdateForm } from '../../_components/seo-page-editors'

/** Props for the SEO update page, providing the route lang segment and the record's id. */
interface SeoUpdatePageProps {
  params: Promise<{ lang: string; id: string }>
}

/** Server-rendered page for editing one page's SEO settings. */
const SeoUpdate = async ({ params }: SeoUpdatePageProps) => {
  const { lang, id } = await params
  const title = (await getServerTranslation(lang, 'page.seo.update.title')) || 'Edit SEO settings'

  return (
    <PageContent title={title} innerClassName="max-w-4xl">
      <SeoPageUpdateForm id={id} />
    </PageContent>
  )
}

export default SeoUpdate
```

A route without a dynamic segment types `params: Promise<{ lang: string }>` and passes nothing down.

`PageContent` is the admin page frame: a header with `title`, an optional `description` line and
`actions` (end-aligned controls such as a primary "create" button), then the body on one card.
`innerClassName` styles that card — constrain form width with it (`max-w-3xl` for most
create/update forms); list and detail screens leave it full width. `plain` drops the card, for a
page that lays out several cards of its own (a detail or settings screen). An account screen uses
`AccountShell` (`title`, `description`, `backHref`) instead, and exports `generateMetadata` returning
its translated title. Colors follow the token rules in the `ui-component` skill.

A **public page** is different: it renders its content in the server component itself (translated
with `getServerTranslation`, no `PageContent`), exports `generateMetadata` returning
`buildMetadata(lang, PATH)` and renders `<JsonLd lang={lang} path={PATH} />` — both from
`@/lib/server/seo`. Interactive islands on it are small client components; the page's text never
waits on the client. Follow the `seo` skill for the whole procedure.

## The client component

`_components/<kebab-name>.tsx`, first line `'use client'`, named export, JSDoc above the component.
Everything interactive lives here: RTK Query hooks, Formik forms, tables, modals. Server pages
never call the API through RTK Query (a public page reads the API only through the server helpers
in `lib/server/`, such as `buildMetadata` and `getServerTranslation`).

Common imports:

```tsx
import { useTranslation } from '@/i18n'
import { useLocalizedRouter, useTableUrlState } from '@/hooks'
import { Button, ApiErrorMessages, Loader, LocalizedLink } from '@/components/ui'
import { FormInput, FormCheckbox } from '@/components/ui/form'
import { apiErrorAlert, successToast, isAllowed } from '@/lib/utils'
import { adminPath } from '@/lib/paths'
import { useAppSelector } from '@/store/hooks'
import { useSeoPageGetQuery } from '@/store/api/seo'
import { Allow } from '@/allow'
```

Anything that loads data renders in this order: `isLoading` → `<Loader />`, `error` →
`<ApiErrorMessages error={error} />`, no data → a translated "not found" line, then the content —
each state centred in a `flex items-center justify-center` wrapper (on its own `panel` when the page
is `plain`).

**Navigation must stay locale-aware** — use `useLocalizedRouter()` instead of `next/navigation`'s
router, and `<LocalizedLink href={adminPath('/users')}>` instead of `next/link`. Pass unprefixed
paths; the helpers add the locale segment (`router.localize(href)` gives the prefixed path for a
full page load).

When an admin save changes something a public page shows — an SEO page, the `Seo` setting, a
translation or the language settings — call `revalidatePublicCache(['seo'])` or
`revalidatePublicCache(['localization'])` (`lib/revalidate-client.ts`) after the mutation succeeds, so
the web server drops its cached copy at once instead of after five minutes.

## Access: public, signed in, permitted

Three layers decide whether a screen opens; register the route with each that applies.

1. **`proxy.ts`** (Next 16's renamed middleware) negotiates the locale and checks only that a
   session cookie exists, and only where `isAuthRequired` in `auth-urls.ts` says one is needed: the
   admin area and the account area (`isAdminPath`/`isAccountPath`). Without a cookie those redirect
   to `/signin?redirect=…`. Every other page — the public site and the anonymous screens — is open,
   so a new public page needs no registration at all, and a new `/admin/…` or `/account/…` screen is
   guarded with none. `anonymousUrls` names the screens that only make sense signed out (it tells a
   signed-in visitor apart from one who is not); a new sign-in-like screen joins it. The proxy does
   not check permissions.
2. **`auth-urls.ts`** names the permissions a guarded route needs. Admin entries are written with the
   `admin(path, ...permissions)` helper, which prefixes `/admin` and puts `Allow.Admin_Access` — the
   area's own gate — in front of the screen's own permission:

   ```ts
   admin('/seo/{id}/update', Allow.Seo_Update),
   ```

   `{id}` in a url acts as a wildcard segment. Two entries must not match the same pathname — the
   matcher throws (`auth-urls.test.ts` pins this). An admin path no entry names still needs
   `Admin.Access` (`getMatchedAuthUrl` falls back to it), so an unlisted admin screen is never open to
   an ordinary visitor — but list it anyway when it needs a permission of its own. Account entries
   (`{ url: accountPath('/profile') }`) name no permission: a session is enough.
3. **`AuthGuard`** (`components/layouts/auth-guard.tsx`), rendered only by `admin/layout.tsx` and
   `(account)/account/layout.tsx`, waits for the account info, then applies `decideGuardRedirect`
   (`components/layouts/guard-redirect.ts`): no session → `/signin?redirect=…`, a missing permission
   from the matched `auth-urls.ts` entry → `/unauthorized`. The public site never renders it, so its
   pages are never held behind the loader; never wrap a `(site)` page in it.

The web gate is convenience, not security: every API endpoint the screen calls checks its own
permission. `Admin.Access` is checked by the web app alone, so it never replaces the endpoint's
`Permissions(...)` — see the `permissions` skill.

In-page, gate actions with `isAllowed(authState, [Allow.X])` — see the `permissions` skill.

## Translations

Add every string to the API's `Features/Localization/Core/Resources/en.json` and to every other
`<code>.json` beside it (the backend's `LocalizationResourceStoreTests` fail when their key sets
differ). Follow
the existing namespaces: `site.<page>.*` and `site.nav.*` for the public site, `page.<area>.*` (or
`page.<area>.<screen>.*`) for account and admin screens, `form.label.*`, `form.placeholder.*`,
`validation.*`, `table.*`, `navigation.*`, `search.*`, `error.server.*`, `common.*`.

Server components use `(await getServerTranslation(lang, key)) || 'English'`; client components use
`const { t } = useTranslation()` and `t('key', { min: 3 }) || 'English'` (placeholders are `${min}` in the JSON;
the default after `||` is the key's `en.json` value, which renders when the key is not served).
Both come from `@/i18n`, never a subpath. The `localization` skill covers the third (non-component)
translator, overrides and adding a language.

## Register the destination

- **Admin screen** — `nav-items.ts` sidebar entry. Items are either a `NavItem` or a `NavItemGroup`
  (`{ title, items }`). `title` is an i18n key, `url` is built with `adminPath(...)`, `icon` comes from
  `lucide-react`, and detail routes are listed as children with `show: false` so they highlight the
  parent (and feed the breadcrumbs) without appearing in the menu. There is no permission field: the
  sidebar drops an item whose `url` exactly matches an `auth-urls.ts` entry the caller fails, and a
  parent whose children are all dropped.
- **Admin screen** — `searchable-items.ts` global search entry (`{ title: 'search.seo', url: adminPath('/seo') }`),
  filtered the same way. Routes with an `{id}` segment are not listed.
- **Public page** — `SITE_LINKS` in `app/[lang]/(site)/_components/site-header.tsx` when it belongs in
  the header (and the footer in `site-footer.tsx` when it belongs there), plus its SEO row and sitemap
  listing per the `seo` skill. Public pages are not in `nav-items.ts` or `searchable-items.ts`, which
  serve the admin shell.

## Checklist

- [ ] `app/[lang]/(site)/<route>/page.tsx`, `app/[lang]/(account)/account/<route>/page.tsx` or
      `app/[lang]/admin/<group>/<route>/page.tsx` (server, default export)
- [ ] `_components/<name>.tsx` (`'use client'`, named export)
- [ ] Admin: an `admin(...)` entry in `auth-urls.ts` naming the screen's permission
- [ ] Public: `generateMetadata` → `buildMetadata(lang, PATH)`, `<JsonLd>`, an SEO row (`seo` skill)
- [ ] `revalidatePublicCache([...])` after admin saves that change what public pages show
- [ ] Keys in every backend resource file (`Features/Localization/Core/Resources/*.json`)
- [ ] Admin: `nav-items.ts` + `searchable-items.ts`; public: `SITE_LINKS` if it is in the header
- [ ] `npm run lint`, `npx tsc --noEmit`, `npm run test` and `npm run build` pass
