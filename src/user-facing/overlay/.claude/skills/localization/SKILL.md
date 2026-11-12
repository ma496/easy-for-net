---
name: localization
description: Work with i18n across the API and the web app — adding translation keys to the backend resource files, the three translator entry points (client hook, server helper, non-component) imported from @/i18n, the flat dictionary the API serves (cached for anonymous visitors), the stored overrides and the admin screen with its public-cache revalidation, the tests that keep every locale in step, adding or removing a language, RTL handling, and how locale-prefixed routing works. Use when adding user-facing text or a new language.
---

# Localization

Translations are served by the API. The shipped strings live in
`src/backend/Source/Features/Localization/Core/Resources/<code>.json` (nested JSON, embedded in the
assembly); `LocalizationResourceStore` flattens each file to dotted keys (`common.save`) once, and
`LanguageCatalog` carries each code's display name and `isRtl`. The web app fetches the merged result
per request and has no message files of its own.

## How a request is translated

`GET /localization/resources/{culture}` (anonymous) returns
`{ culture, defaultCulture, languages, resources }` — the culture actually served, the site's default,
its enabled languages, and a **flat** dotted-key dictionary. Every caller — anonymous or signed in —
gets the same answer for the same culture. Each key resolves first answer
wins:

1. the stored override (a `LocalizationText` row for that key and culture);
2. the shipped value for the served culture;
3. the shipped English value.

Which languages the site offers resolves as a unit: the one `LanguageSetting` row when it is stored
→ every shipped culture enabled, no default. The culture served is the one requested if enabled, else
the default, else `en`, else the first enabled one. `ICultureCatalog` (the shipped cultures) and
`ILanguageProvider` (the enabled set and default) are the `[AllowOutside]` contracts other slices
read — the SEO page validator and the sitemap use them; `ILocalizationService` stays private to the
slice.

On the web, `getDictionary(locale)` in `i18n/server.ts` is a `cache()`d server fetch of that endpoint —
one fetch per request, shared by the root layout and every `getServerTranslation` call. How it is
fetched is `dictionaryFetchInit`: a visitor with no session cookie gets the same answer as every other
anonymous visitor, so the fetch sends no cookies and is cached by Next for five minutes under the
`localization` tag (`LOCALIZATION_CACHE_TAG`) — which is what keeps the public pages fast; a signed-in
visitor's fetch forwards their cookies and is never cached (`cache: 'no-store'`), so an administrator
sees their own edit on the next request. `i18n/dictionary-fetch.test.ts` pins both. The root layout hands the whole response to `TranslationProvider`, which
publishes it to `useTranslation` (via context) and `getTranslation` (via a module-level copy). When the
API cannot be reached the dictionary is empty, and every call site's own English default renders (see
below) — the web app bundles no message files.

## Three ways to translate

| Where | Use | Import |
| --- | --- | --- |
| Client component | `const { t, i18n } = useTranslation()` then `t('page.users.title') || 'Users'` | `@/i18n` |
| Server component | `(await getServerTranslation(lang, 'page.users.title')) || 'Users'` | `@/i18n` |
| Non-component module (alerts, utils) | `const { t } = getTranslation()` then `t('common.error') || 'Error'` | `@/i18n` |

**Always import from the `@/i18n` barrel, never from `@/i18n/server`, `@/i18n/client` or another
subpath.** The barrel is safe in client components because `server.ts` loads `next/headers` lazily
inside `getDictionary`; keep it that way — a top-level `next/headers` import there breaks every client
bundle.

`useTranslation` also returns `i18n.language` (the locale from the URL) and `i18n.changeLanguage(code)`.
`getTranslation` reads the dictionary `TranslationProvider` published, so it only works client-side
after the provider mounted and does **not** react to a language change without a reload. Prefer
passing already-translated text into helpers where you can.

All three look the key up in the flat dictionary (`i18n/translate.ts`) and return **`''`** when it is
missing, so **every call carries its English default after `||`** — the key's exact value in `en.json`.
That default is what renders when the API is down or the key is not served; `isTranslationKeyExist(key)`
(`@/lib/utils`) tells optional copy apart. `i18n/default-texts.test.ts` fails when a call with a
literal key has no default, or a plain-string default differs from `en.json` — so when you change an
English value, change its defaults too.

- **Precedence.** `||` binds looser than `.`, `+`, `===`, `&&` and `??`: write
  `(t('k') || 'X').toLowerCase()` and `a && (t('k') || 'X')`. A server call is awaited inside
  parentheses — `(await getServerTranslation(lang, 'k')) || 'X'` — or, inside `Promise.all`,
  `getServerTranslation(lang, 'k').then((text) => text || 'X')`.
- **Variables.** Interpolate the default yourself, with the same values:
  `` t('page.users.count', { count }) || `${count} users` ``.
- **A key known only at run time** (an enum value, a key held in data such as `nav-items.ts`) falls back
  to `humanizeKey` (`@/i18n`), which sentence-cases the last segment:
  `` t(`page.sales.status.${status}`) || humanizeKey(status) ``. Prefer literal keys where the set is
  small — a ternary of two `t('…')` calls rather than `t(cond ? 'a' : 'b')` — so each gets its exact English.

Interpolation is `${name}` in the JSON value:

```json
"validation": { "minLength": "Field must be at least ${min} characters" }
```

```tsx
t('validation.minLength', { min: 3 }) || 'Field must be at least 3 characters'
t('page.users.count', { count }) || `${count} users`
```

## Adding keys

Add the key to `Features/Localization/Core/Resources/en.json` first — English declares the key set
every other file mirrors — then to **every other resource file in that directory**, translated.
`Tests/Features/Localization/Core/LocalizationResourceStoreTests` (run by `dotnet test`) fails when:

- `en` is not shipped, or a shipped file has no `LanguageCatalog` entry;
- any file is missing a key English has, or has one English lacks;
- a value is empty, or is the key itself.

Placeholders are not checked, so keep every `${…}` of the English value in each translation yourself.

Follow the established namespaces:

`brand.*`, `common.*`, `cookieConsent.*`, `site.<page>.*` and `site.nav.*` (the public site),
`navigation.*`, `search.*`, `page.<area>.*` (or
`page.<area>.<screen>.*`), `form.label.*`, `form.placeholder.*`, `validation.*`,
`table.columns.*` / `table.filter.*` / `table.export.*` / `table.actions` / `table.createLink`,
`file.*`, `error.<status>.*` and `error.server.<errorCode>`, `notifications.*`.

Two namespaces are contracts with the backend, not free-form copy:

- `error.server.<code>` must match a constant in `ErrorHandling/ErrorCodes.cs` — see the
  `api-error-handling` skill.
- `notifications.<name>.title` / `.message` must match the `TitleKey`/`MessageKey` strings a
  notification is created with — see the `notifications` skill.

Never hard-code user-facing strings in components; the only literals in JSX should be keys and the
English default after each `t('…') ||`.

## Overrides and language settings

Administrators change copy without a deploy on `app/[lang]/admin/localization` (permissions
`Localization.View` / `Localization.Update`). There is one layer of overrides, the site's own:

| Endpoint | Does |
| --- | --- |
| `GET /localization/texts` | One page of keys for a culture: shipped (`defaultValue`), what stands without an override (`inheritedValue`) and the override (`value`) |
| `PUT` / `DELETE /localization/texts` | Set or remove the override of one key |
| `GET /localization/languages` | Shipped languages, the enabled set and default, and whether they are the stored row or the shipped fallback (`isInherited`) |
| `PUT` / `DELETE /localization/languages` | Set or remove the `LanguageSetting` row |

Overrides are keyed by the shipped key set: a key that is not in `en.json` cannot be edited, so new
copy always starts as a resource-file entry. After a save the screen calls
`revalidatePublicCache(['localization'])` (`lib/revalidate-client.ts`) — which drops the cached
anonymous dictionary, so the public pages show the edit on their next request rather than when the
five minutes run out — and `router.refresh()`, so the root layout re-fetches the dictionary for the
administrator. Any new screen that writes texts or language settings does both. Sign-in also calls
`router.refresh()`, since a signed-in visitor's dictionary is fetched differently.

## Routing

Routable locales are `i18nConfig.locales` in `i18n/config.ts` (`defaultLocale: 'en'`). The default
locale's URLs carry no prefix — `proxy.ts` rewrites internally for it and redirects for the others,
so `/users` and `/ar/users` are both valid while `/en/users`
redirects to the unprefixed form. For a URL with no locale, `i18n/resolve-locale.ts` picks, first
answer wins: the `preferred-language` cookie (written by `LanguageDropdown`) → the `scope-language`
cookie → `Accept-Language` → the default, counting only cultures in the `scope-languages` cookie
(the site's enabled set). A URL whose locale is routable but not enabled is redirected to the
resolved one. The whole decision is the pure `decideLocaleRouting` in `i18n/locale-routing.ts`, which
`proxy.ts` only wraps.

`LocaleGuard` (`components/layouts/locale-guard.tsx`, decision in `i18n/locale-guard.ts`) runs inside
the provider: when the served culture differs from the URL's locale (not enabled on the site) it
navigates to the served one; when the visitor has no `preferred-language` cookie and the site has a
default that differs, it navigates to that default. It only ever targets a culture that is enabled and routable,
keeps the `scope-language` (default) and `scope-languages` (enabled set) cookies in step, and clears a
`preferred-language` cookie naming a culture the site does not enable — which is what keeps the proxy
and the API agreeing. `i18n/locale-routing-loop.test.ts` replays proxy → API → guard hops over a matrix
of language settings and visitors and fails when any combination does not settle; extend it when the rule changes. Links and navigation stay locale-aware through `LocalizedLink` and
`useLocalizedRouter()`.

The language dropdown lists `languages` from the dictionary, so a site that enables a subset offers
only that subset. The sitemap and every public page's hreflang alternates list the enabled cultures
that are also routable (see the `seo` skill), so enabling or disabling a language changes them too.

## Adding a language

1. Add `Features/Localization/Core/Resources/<code>.json` — start from `en.json` so every key exists,
   then translate every value (the test rejects a value equal to its key, not one equal to the English
   text, so review it).
2. Add `{ name, isRtl }` for the code to `LanguageCatalog` (`Features/Localization/Core/LanguageCatalog.cs`).
3. Add the code to `i18nConfig.locales` in `i18n/config.ts` — `i18n/locales.test.ts` fails when that
   list differs from the backend's resource files.
4. Make sure `public/assets/images/flags/<CODE>.svg` exists — `LanguageDropdown` loads the flag by the
   upper-cased code.

Removing a language is the same four places in reverse. Stored `LanguageSetting` rows that still
enable the removed code are not rewritten, so re-save the languages from the admin
screen (the update endpoint accepts only shipped cultures).

## RTL

The served language's `isRtl` (from `LanguageCatalog`, delivered in `languages`) drives
the document direction: the root layout writes `dir` on `<html>` in the first HTML (so a public page
is right-to-left before any script runs), and `themeConfigSlice.rtlClass` follows it on the client. Consequently **all component styling must be direction-agnostic**:
use `ms-`/`me-`, `ps-`/`pe-`, `inset-s-`/`inset-e-`, `text-start`/`text-end`, `border-s`/`border-e`,
`rounded-s`/`rounded-e` rather than `ml-`/`pl-`/`left-`/`text-left`, or pair `ltr:`/`rtl:` variants
where no logical utility exists. Components that must branch read
`useAppSelector((state) => state.theme.rtlClass) === 'rtl'` — the anchor side of
`DataTableExportButton` and `DataTableRowActions` is the existing example. No test scans for physical
utilities, so review is what keeps them out.

## Single-language projects

`dotnet efn cp` defaults to `-m false`, which deletes the non-English resource files, reduces
`locales` in `i18n/config.ts` to `['en']`.
In such a project there is exactly one resource file to update — but keep using `t()` and keys
anyway, so adding a language later is only the four steps above.
The generator rewrites `config.ts` with a regular expression matching `locales: [ … ]`, so keep that
property spelled exactly so (one space after the colon) with no `]` inside the array.

## Checklist

- [ ] Every new string is a key, added and translated in every backend resource file
- [ ] Key placed in the right namespace; backend-coupled keys match their constants
- [ ] Translators imported from `@/i18n`; server components use `getServerTranslation`, client
      components `useTranslation`
- [ ] New markup uses logical (RTL-safe) utilities
- [ ] Every `t('…')` / `getServerTranslation` call carries its English default after `||`
- [ ] A new language is registered in all four places, flag included
- [ ] Admin writes to texts or languages call `revalidatePublicCache(['localization'])` and `router.refresh()`
- [ ] `dotnet test` (resource-file test) and `npm run test` (locale, default-text and dictionary-fetch tests) pass
