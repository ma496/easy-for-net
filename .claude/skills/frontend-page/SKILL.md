---
name: frontend-page
description: Add a route to the Next.js app under src/frontend/web/app/[lang] — server page component, _components client component, locale-prefixed routing and proxy.ts auth gating, i18n keys, nav-items and searchable-items registration. Use for any new screen that is not a full CRUD set.
---

# Adding a page

## Route layout

Every route is locale-prefixed: `app/[lang]/…`. Route groups:

- `(public)` — marketing/landing, no auth
- `(auth)` — signin, signup, profile, password flows
- `admin/` — the authenticated app shell (sidebar, header, settings); `admin/(identity)` groups the
  identity screens without adding a URL segment

A screen is a folder with `page.tsx` plus a sibling `_components/` folder holding the interactive
client parts. Dynamic segments are folders: `update/[id]/page.tsx`.

## The page component (server)

`page.tsx` is a **server** component. It awaits `params`, resolves the title with
`getServerTranslation`, and renders the client component inside `AdminPageContent`:

```tsx
import { getServerTranslation } from '@/i18n'
import { UserTable } from './_components/user-table'
import { AdminPageContent } from '@/components/layouts'

/** Props for the users list page, providing the localized route lang segment. */
interface UsersProps {
  params: Promise<{ lang: string }>
}

/** Server-rendered users list page… */
const Users = async ({ params }: UsersProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.users.title')
  return (
    <AdminPageContent title={title}>
      <UserTable />
    </AdminPageContent>
  )
}

export default Users
```

For a dynamic route, destructure the extra segment and pass it down:
`const { lang, id } = await params` → `<UserUpdateForm userId={id} />`.

`AdminPageContent` takes `title` and an optional `innerClassName` to constrain form width
(`max-w-187.5` for create, `max-w-155` for update are the values in use).

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
import { Allow } from '@/allow'
```

**Navigation must stay locale-aware** — use `useLocalizedRouter()` instead of `next/navigation`'s
router, and `<LocalizedLink href="/admin/users/list">` instead of `next/link`. Pass unprefixed
paths; the helpers add the locale segment.

## Auth gating

`proxy.ts` (Next 16's renamed middleware) handles locale negotiation *and* auth. It reads
`auth-urls.ts`: anything under `/admin/` requires a session, and an entry with `permissions`
requires those permissions. Add your route there:

```ts
{ url: '/admin/notifications/list', permissions: [Allow.Notification_View] },
```

`{id}` in a url acts as a wildcard segment. Two entries must not match the same pathname — the
matcher throws. In-page, gate buttons with `isAllowed(authState, [Allow.X])`; see the `permissions`
skill.

## Translations

Add every string to `public/locales/en.json` — and to every other `public/locales/<code>.json`
when the project is multi-language (`i18n/config.ts` lists the locales). Follow the existing key
namespaces: `page.<area>.*`, `form.label.*`, `form.placeholder.*`, `validation.*`, `table.*`,
`navigation.*`, `search.*`, `error.server.*`, `common.*`.

Server components use `await getServerTranslation(lang, key)`; client components use
`const { t } = useTranslation()` and `t('key', { min: 3 })` (placeholders are `${min}` in the JSON).
The `localization` skill covers the key namespaces, the third (non-component) translator, and adding
a language.

## Register the destination

- `nav-items.ts` — sidebar entry. Items are either a `NavItem` or a `NavItemGroup` (`{ title, items }`).
  `title` is an i18n key, `url` is unprefixed, `icon` comes from `lucide-react`, and detail routes are
  listed as children with `show: false` so they highlight the parent without appearing in the menu.
- `searchable-items.ts` — global search entry (`{ title: 'search.users', url: '/admin/users/list' }`).

## Checklist

- [ ] `app/[lang]/<group>/<route>/page.tsx` (server, default export)
- [ ] `_components/<name>.tsx` (`'use client'`, named export)
- [ ] `auth-urls.ts` entry if the route is guarded
- [ ] Keys in `public/locales/*.json`
- [ ] `nav-items.ts` + `searchable-items.ts`
- [ ] `npm run lint` and `npm run build` pass
