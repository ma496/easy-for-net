---
name: seo
description: Add or change a public, search-indexed page and its metadata — a server component under app/[lang]/(site)/ with generateMetadata → buildMetadata(lang, path) and <JsonLd>, the page's SeoPage row (admin screen or DataSeeder), {placeholder} rows for dynamic paths, how /seo/resolve merges culture, neutral and site defaults, the sitemap and robots rules, the SEO cache and revalidation after saves, the Seo setting, and the tests to touch. Use for any public page, any change to the Seo slice, lib/seo, lib/server/seo.tsx, app/sitemap.ts or app/robots.txt.
---

# Public pages and SEO

The public site is `app/[lang]/(site)/`: server components rendered with their content in the first
HTML, open to everyone (`isAuthRequired` in `auth-urls.ts` covers only `/admin` and `/account`). What
a page says about itself to search engines and social networks — title, description, canonical,
hreflang alternates, Open Graph and X cards, robots directives, JSON-LD — is **data**, a `SeoPage` row
the administrator edits on `/admin/seo`, never text in the page's code. The page's code owns its body
copy (translations, `site.<page>.*` keys) and nothing else.

## 1. Add a public page

```tsx
// app/[lang]/(site)/pricing/page.tsx
import { getServerTranslation } from '@/i18n'
import { buildMetadata, JsonLd } from '@/lib/server/seo'

/** The pricing page's address, as its SEO record is keyed. */
const PATH = '/pricing'

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params
  return buildMetadata(lang, PATH)
}

/** The public pricing page, rendered on the server. */
const Pricing = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'site.pricing.title')) || 'Pricing'
  return (
    <>
      <JsonLd lang={lang} path={PATH} />
      <div className="mx-auto max-w-4xl px-4 py-14 sm:px-6 sm:py-20">
        <h1 className="text-3xl font-semibold tracking-tight text-foreground">{title}</h1>
      </div>
    </>
  )
}

export default Pricing
```

Rules, all followed by `page.tsx`, `about/page.tsx` and `contact/page.tsx`:

- A server component, no `'use client'`. Interactive parts go in a client child; the content a crawler
  must see stays in the server-rendered tree. No `AuthGuard`, no RTK Query, nothing that waits on the
  visitor's session — the `(site)` layout renders immediately and the header fills in the account menu
  later.
- `PATH` is the locale-free path, `/` for the home page, exactly as the `SeoPage` row is keyed.
  `buildMetadata` returns an absolute title (so the root layout's `%s | brand` template does not apply
  twice), the canonical and one hreflang alternate per served culture plus `x-default`, and falls back
  to the brand name alone when the API cannot be reached — the page still renders.
- `<JsonLd lang path />` renders the row's structured data as `<script type="application/ld+json">`
  (re-serialized and `<`-escaped by `safeJsonLd`), or nothing. `resolveSeo` is `cache()`d per request, so
  `generateMetadata` and `<JsonLd>` share one fetch.
- Body text goes through `getServerTranslation(lang, 'site.<page>.…') || 'English'`, the key added to
  every backend resource file (`localization` skill). Do not put the `<title>` or meta description in
  translations — those are the SEO row's.
- Link the page from `app/[lang]/(site)/_components/site-header.tsx` (its `links` list, key
  `site.nav.<page>`) and the footer where it belongs, with `LocalizedLink` and a locale-free `href`.
- Never add the page to `auth-urls.ts`, `nav-items.ts` or `searchable-items.ts` — those are the admin
  area's.

## 2. Give it an SEO row

A path with no row still renders: `/seo/resolve` answers the `Seo` setting's defaults (site name as
title, default description and image). Give a real page its own row, one of two ways:

- **Shipped with the app** — add it to `pages` in `DataSeeder.SeedSeoPagesAsync`
  (`("/pricing", "Pricing", "What it costs.")`). The seeder inserts a culture-neutral row only when no
  row for that path exists, deleted ones included, so a page an administrator removed is not brought
  back, and editing the seeded text later changes nothing for an existing database.
- **Content an administrator manages** — `/admin/seo/create` (`Seo.Update`) writes it through
  `POST /seo/pages`.

`SeoPage` (`Features/Seo/Core/Entities`, table `seo.SeoPages`, `ISoftDelete`): `Path`, `Culture`
(null = every language), `Title` (before the site's title template), `Description`, `Keywords`
(comma-separated), `CanonicalUrl` (absolute http(s); blank = the page's own URL), `OgTitle`,
`OgDescription`, `OgImageFileId` (a stored file, served by `GET /seo/images/{fileId}`), `TwitterCard`
(`summary` | `summary_large_image`), `NoIndex`, `NoFollow`, `IncludeInSitemap` (default true),
`ChangeFrequency` (the sitemap protocol's words), `Priority` (0–1), `JsonLd` (a JSON object or array,
`invalidJsonLd` otherwise). One live row per (path, culture) — a unique filtered index with nulls not
distinct, answered first by `seoPageAlreadyExists`. Blank text is stored as null, so that field falls
back. Bounds are `SeoConstants`.

### How a page resolves

`GET /seo/resolve?path=/about&culture=fr` (anonymous) finds the row set by the **exact path**, else by
the **most specific template** that matches it (fewest placeholders, then ordinal path). Within that set
each field is the first non-blank of: the row for the requested culture → the culture-neutral row →
the `Seo` setting (description, image, Twitter site). `Index` is false when `AllowIndexing` is off or the
primary row says `NoIndex`. A culture that is not shipped reads as the neutral rows. Every URL in the
answer is absolute, on `SeoSettings.SiteUrl`, else `Web:DefaultDomain`.

## 3. Dynamic paths

A page with a route parameter is one template row: path `/blog/{slug}` (a whole segment
`{name}`, letters/digits/underscore) stands for every `/blog/<anything>` of the same segment count.

```tsx
// app/[lang]/(site)/blog/[slug]/page.tsx
export async function generateMetadata({ params }: { params: Promise<{ lang: string; slug: string }> }) {
  const { lang, slug } = await params
  return buildMetadata(lang, `/blog/${slug}`)   // the concrete path; the API matches the template
}
```

A concrete row (`/blog/launch-day`) beats the template for that one path. A template row is never
listed in the sitemap (there is no concrete URL to list); a post that should be listed gets its own
concrete row, or `app/sitemap.ts` is extended with entries from the feature that owns the content.
When the per-item title or description should come from the content itself rather than the template,
merge it in the page's own `generateMetadata` over what `buildMetadata` returned — keep the canonical
and alternates `buildMetadata` built.

## 4. Sitemap and robots

Both are rendered on request (`dynamic = 'force-dynamic'`), never at build time.

- `app/sitemap.ts` → `GET /seo/sitemap` → `toSitemap` (`lib/seo/sitemap.ts`): one entry per listed path
  per routable culture (the default culture unprefixed), each with every culture's address as its
  alternates. The API lists a path when one of its live rows has `IncludeInSitemap` and not `NoIndex`
  and it is not a template; frequency and priority from its neutral row when that qualifies, else the
  first culture row; last-modified the latest of them. `AllowIndexing` off lists nothing. API down →
  empty sitemap.
- `app/robots.txt/route.ts` → `GET /seo/robots` → `toRobotsTxt` (`lib/seo/robots.ts`): `Allow: /` plus
  `Disallow` for `ALWAYS_DISALLOWED` (`/admin`, `/account`, `/api`) when indexing is allowed, else
  `Disallow: /`; the sitemap's address; then `RobotsExtra` verbatim. API down → everything disallowed.
  A route handler, not `robots.ts`, because the extra lines are raw text.
- The admin layout and the account layout export `robots: { index: false, follow: false }`. A new area
  that is not for search engines does the same and, if it has its own top-level segment, joins
  `ALWAYS_DISALLOWED`.

## 5. Caching and revalidation

Two caches sit between an edit and a public page:

- **API**: `ISeoCache` (`SeoCache`, in-memory, singleton) holds what `/seo/resolve` reads, per culture,
  for at most 10 minutes. Every SEO page write calls `seoCache.Invalidate()` after saving, and
  `SeoCache` is also an `ISettingChangedHandler` that invalidates when the `Seo` setting changes. A new
  endpoint that writes `SeoPages` must call `Invalidate()` too. Each API instance has its own cache, so
  behind several instances an edit reaches the others when their entries expire.
- **Web server**: `lib/server/seo.tsx` fetches the `/seo` endpoints with no cookies and no forwarded host,
  `next: { revalidate: 300, tags: ['seo'] }` — the same answer for every visitor. After a save that
  changes a public page (an SEO row, the `Seo` setting), the admin screen calls
  `await revalidatePublicCache(['seo'])` (`lib/revalidate-client.ts`) before navigating away. It posts
  to `app/[lang]/admin/revalidate/route.ts`, which asks `GET /account/get-info` with the caller's
  cookies and clears a tag only for a caller holding its permission (`decideRevalidation` in
  `lib/revalidate-rule.ts`: `seo` → `Seo.Update`, `localization` → `Localization.Update`). Best effort:
  a failed revalidation means the old copy lives out its five minutes. A new tag goes into
  `REVALIDATABLE_TAGS` with the permission that may clear it — never accept a tag without one.

## 6. The `Seo` setting

`SeoSettings` (`Features/Seo/Core`, registered as `Seo` by `SeoSettingsProvider`, edited on the SEO tab
of `/admin/settings` under `Settings.Update`): `SiteName`, `TitleTemplate` (must contain `%s`),
`DefaultDescription`, `DefaultOgImageFileId` (empty or a file id), `TwitterSite`, `SiteUrl` (empty →
`Web:DefaultDomain`; must be absolute http(s)), `AllowIndexing` (off for staging: every page `noindex`,
empty sitemap, `Disallow: /`), `RobotsExtra`. Read it through `ISeoSiteService.GetSettingsAsync`, which
also builds `SiteUrl(settings)` and `ImageUrl(siteUrl, fileId)`. See the `settings` skill for adding a
property.

## 7. The public endpoints stay narrow

`/seo/resolve`, `/seo/sitemap`, `/seo/robots` and `/seo/images/{fileId}` are `AllowAnonymous()`. Anything
added to them is readable by anyone, so:

- they return only what a public page's `<head>` would show anyway — never audit fields, ids of
  unrelated rows, or anything from another slice;
- the image endpoint serves a file only while a live row or the setting references it, and answers 404
  for everything else (see `file-storage`);
- caching by request input must stay bounded: resolve caches per shipped culture, never per path.

The admin endpoints (`/seo/pages`, `SeoPagesGroup`) check `Seo.View` / `Seo.Update`; the screens under
`/admin/seo` also need `Admin.Access` through `auth-urls.ts`.

## 8. Tests to touch

- **API** (`Tests/Features/Seo`, derive from `SeoTestsBase`, which shares the `Settings` collection and
  drops the SEO cache after each test): give every page its own `NewPath()`, read anonymously with
  `AnonymousClient()` / `ResolveAsync` / `SitemapAsync`, arrange with `CreatePageAsync` and `SetSeoAsync`.
  `SeoResolveTests` (fallback chain, templates, absolute URLs, indexing), `SeoSitemapRobotsTests`,
  `SeoImageTests` (only referenced files served), `SeoPageCrudTests` (permissions, validation,
  `Seeded_Pages_Exist` — extend it when the seeder gains a page).
- **Web** (Vitest): `lib/seo/metadata.test.ts` (`toMetadata`, `localizedPath`, `safeJsonLd`),
  `lib/seo/sitemap-robots.test.ts`, `lib/revalidate-rule.test.ts` for a new tag,
  `app/[lang]/admin/seo/_components/seo-page-form-values.test.ts` for a new form field,
  `auth-urls.test.ts` if a screen is added.
- `i18n/default-texts.test.ts` checks every literal-key translator call on the new page carries its
  English default.

## Checklist

- [ ] Page is a server component under `app/[lang]/(site)/` with `generateMetadata` → `buildMetadata(lang, PATH)` and `<JsonLd lang={lang} path={PATH} />`
- [ ] No title or description hard-coded or translated for the `<head>`; body copy under `site.<page>.*` in every resource file
- [ ] An SEO row exists: seeded in `DataSeeder.SeedSeoPagesAsync` (and `Seeded_Pages_Exist` updated) or left to the admin screen deliberately
- [ ] Dynamic route keyed by a `{placeholder}` template row; sitemap entries for its items considered
- [ ] Linked from the site header/footer with a locale-free `LocalizedLink`
- [ ] Any new write to `SeoPages` calls `ISeoCache.Invalidate()`; any admin save that changes a public page calls `revalidatePublicCache`
- [ ] Anonymous SEO endpoints return nothing a page's `<head>` would not show
- [ ] Areas not for search engines export `robots: { index: false, follow: false }` and are in `ALWAYS_DISALLOWED`
