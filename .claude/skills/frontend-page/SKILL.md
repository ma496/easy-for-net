---
name: frontend-page
description: Add a route to the Next.js app under src/frontend/web/app/[lang] — server page component, _components client component, locale-prefixed routing, the three access layers (proxy.ts sign-in check, auth-urls.ts permissions, tenant-scope routing), i18n keys, nav-items and searchable-items registration. Use for any new screen that is not a full CRUD set.
---

# Adding a page

## Route layout

Every route is locale-prefixed: `app/[lang]/…`. Route groups:

- `(public)` — marketing/landing, no auth
- `(auth)` — signin, signup, profile, password flows, `select-tenant`, `unauthorized`
- `admin/` — the authenticated app shell (sidebar, header, settings); nested route groups such as
  `admin/(identity)` and `admin/(tenancy)` group a feature's screens without adding a URL segment

A screen is a folder with `page.tsx` plus a sibling `_components/` folder holding the interactive
client parts. Dynamic segments are folders, and a screen about one record puts the id first:
`[id]/update/page.tsx`, `[id]/detail/page.tsx` (`/admin/tenants/{id}/detail`). A client
component shared by several screens of one group sits in the group's own `_components/`
(`admin/(tenancy)/_components/feature-value-editor.tsx`).

## The page component (server)

`page.tsx` is a **server** component. It awaits `params`, resolves the title with
`getServerTranslation`, and renders the client component inside `AdminPageContent`:

```tsx
import { getServerTranslation } from '@/i18n'
import { TenantDetailView } from './_components/tenant-detail-view'
import { AdminPageContent } from '@/components/layouts'

/** Props for the tenant detail page, providing the route lang segment and the id of the tenant being shown. */
interface TenantDetailPageProps {
  params: Promise<{
    lang: string
    id: string
  }>
}

/** Server-rendered tenant detail page that resolves the localized title and renders the detail view. */
const TenantDetail = async ({ params }: TenantDetailPageProps) => {
  const { lang, id } = await params
  const title = await getServerTranslation(lang, 'page.tenants.detail.title')

  return (
    <AdminPageContent title={title}>
      <TenantDetailView tenantId={id} />
    </AdminPageContent>
  )
}

export default TenantDetail
```

A route without a dynamic segment types `params: Promise<{ lang: string }>` and passes nothing down.

`AdminPageContent` is the page frame: a header with `title`, an optional `description` line and
`actions` (end-aligned controls such as a primary "create" button), then the body on one card.
`innerClassName` styles that card — constrain form width with it (`max-w-3xl` for most
create/update forms); list and detail screens leave it full width. `plain` drops the card, for a
page that lays out several cards of its own (a detail or settings screen). Colors follow the
token rules in the `ui-component` skill.

## The client component

`_components/<kebab-name>.tsx`, first line `'use client'`, named export, JSDoc above the component.
Everything interactive lives here: RTK Query hooks, Formik forms, tables, modals. Server pages
never call the API directly.

Common imports:

```tsx
import { useTranslation } from '@/i18n'
import { useLocalizedRouter, useTableUrlState } from '@/hooks'
import { Button, ApiErrorMessages, Loader, LocalizedLink } from '@/components/ui'
import { FormInput, FormCheckbox } from '@/components/ui/form'
import { apiErrorAlert, successToast, isAllowed } from '@/lib/utils'
import { useAppSelector } from '@/store/hooks'
import { useTenantGetQuery } from '@/store/api/tenancy'
import { Allow } from '@/allow'
```

Anything that loads data renders in this order: `isLoading` → `<Loader />`, `error` →
`<ApiErrorMessages error={error} />`, no data → a translated "not found" line, then the content —
each state wrapped in `<div className="flex justify-center items-center">`.

**Navigation must stay locale-aware** — use `useLocalizedRouter()` instead of `next/navigation`'s
router, and `<LocalizedLink href="/admin/users">` instead of `next/link`. Pass unprefixed
paths; the helpers add the locale segment (`router.localize(href)` gives the prefixed path for a
full page load).

## Access: sign-in, permissions, tenant scope

Three layers decide whether a screen opens; register the route with each that applies.

1. **`proxy.ts`** (Next 16's renamed middleware) negotiates the locale and checks only that a
   session cookie exists: anything under `/admin/`, or listed in `auth-urls.ts`, redirects to
   `/signin?redirect=…` without one. It does not check permissions.
2. **`auth-urls.ts`** names the permissions a route needs. `App.tsx` (the client root guard) matches
   the locale-stripped path with `getMatchedAuthUrl` and sends a caller lacking them to
   `/unauthorized`; the sidebar and global search hide the same entries.

   ```ts
   { url: '/admin/tenants/{id}/detail', permissions: [Allow.Tenant_Detail] },
   ```

   `{id}` in a url acts as a wildcard segment. Two entries must not match the same pathname — the
   matcher throws (`auth-urls.test.ts` pins this). A screen that depends on the tenant's plan needs
   nothing extra here: the API gates its permission with `RequireFeatures`, so the permission is
   simply absent from a session whose plan withholds it — see `permissions` and `feature-management`.
3. **Tenant scope** — `lib/utils/tenant-routing.ts` decides which scope a path belongs to, and
   `isPathAvailable(user, path)` is what `App.tsx`, the sidebar and search consult. Every `/admin`
   screen is **tenant-only by default**: a platform account acting in no tenant is sent to `/admin`
   and never sees it in the menu. Add the path prefix to `platformAccessiblePathPrefixes` if the
   screen also answers in platform scope, or to `platformOnlyPathPrefixes` if it belongs to the
   platform alone (a caller acting inside a tenant is then kept out) — or to
   `platformOnlyPathPatterns` for a platform-only screen about one record, whose id comes before
   the screen so no prefix can name it (`/admin/tenants/{id}/features`). See the `multi-tenancy` skill.

In-page, gate actions with `isAllowed(authState, [Allow.X])` — see the `permissions` skill. Reach for
`useFeature(FeatureNames.X)` (`@/hooks`, `@/feature-names`) only where there is no permission to gate
on, such as a numeric limit or an upsell panel.

## Translations

Add every string to the API's `Features/Localization/Core/Resources/en.json` and to every other
`<code>.json` beside it (the backend's `LocalizationResourceStoreTests` fail when their key sets
differ). Follow
the existing namespaces: `page.<area>.*` (or `page.<area>.<screen>.*`), `form.label.*`,
`form.placeholder.*`, `validation.*`, `table.*`, `navigation.*`, `search.*`, `error.server.*`,
`common.*`.

Server components use `await getServerTranslation(lang, key)`; client components use
`const { t } = useTranslation()` and `t('key', { min: 3 })` (placeholders are `${min}` in the JSON).
Both come from `@/i18n`, never a subpath. The `localization` skill covers the third (non-component)
translator, overrides and adding a language.

## Register the destination

- `nav-items.ts` — sidebar entry. Items are either a `NavItem` or a `NavItemGroup` (`{ title, items }`).
  `title` is an i18n key, `url` is unprefixed, `icon` comes from `lucide-react`, and detail routes are
  listed as children with `show: false` so they highlight the parent (and feed the breadcrumbs)
  without appearing in the menu. There is no permission field: the sidebar drops an item whose `url`
  exactly matches an `auth-urls.ts` entry the caller fails or that `isPathAvailable` rejects, and a
  parent whose children are all dropped.
- `searchable-items.ts` — global search entry (`{ title: 'search.users', url: '/admin/users' }`),
  filtered the same way. Routes with an `{id}` segment are not listed.

## Checklist

- [ ] `app/[lang]/<group>/<route>/page.tsx` (server, default export)
- [ ] `_components/<name>.tsx` (`'use client'`, named export)
- [ ] `auth-urls.ts` entry if the route needs a permission
- [ ] Scope registered in `lib/utils/tenant-routing.ts` if the screen is not tenant-only
- [ ] Keys in every backend resource file (`Features/Localization/Core/Resources/*.json`)
- [ ] `nav-items.ts` + `searchable-items.ts`
- [ ] `npm run lint`, `npx tsc --noEmit`, `npm run test` and `npm run build` pass
