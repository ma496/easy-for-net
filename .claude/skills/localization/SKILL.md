---
name: localization
description: Work with i18n across the API and the web app — adding translation keys to the backend resource files, the three translator entry points (client hook, server helper, non-component) imported from @/i18n, the flat dictionary the API serves, tenant and platform overrides and the admin screen, the tests that keep every locale in step, adding or removing a language, RTL handling, and how locale-prefixed routing works. Use when adding user-facing text or a new language.
---

# Localization

Translations are served by the API. The shipped strings live in
`src/backend/Source/Features/Localization/Core/Resources/<code>.json` (nested JSON, embedded in the
assembly); `LocalizationResourceStore` flattens each file to dotted keys (`common.save`) once, and
`LanguageCatalog` carries each code's display name and `isRtl`. The web app fetches the merged result
per request and has no message files of its own.

## How a request is translated

`GET /localization/resources/{culture}` (anonymous) returns
`{ culture, defaultCulture, languages, resources }` — the culture actually served, the acting scope's
default, its enabled languages, and a **flat** dotted-key dictionary. Each key resolves first answer
wins:

1. the acting tenant's override (`LocalizationText` row with that `TenantId`);
2. the platform's override (`TenantId == null`);
3. the shipped value for the served culture;
4. the shipped English value.

Which languages a scope offers resolves per row, not per key: the tenant's own `LanguageSetting`
row → the platform's → every shipped culture enabled, no default. The culture served is the one
requested if enabled, else the scope's default, else `en`, else the first enabled one. A session acting
in no tenant sees platform overrides only. An anonymous caller on a tenant host sees that tenant's texts
and language settings — the endpoint names the tenant to `ILocalizationService` explicitly and opens no
tenant scope — and anywhere else (the platform host, an unknown tenant's host, no host resolution) the
platform's only.

On the web, `getDictionary(locale)` in `i18n/server.ts` is a `cache()`d server fetch of that endpoint
that forwards the request's cookies — one fetch per request, shared by the root layout and every
`getServerTranslation` call. The root layout hands the whole response to `TranslationProvider`, which
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
t('page.auth.signin.titleTenant', { name }) || `Sign in to ${name}`
```

## Adding keys

Add the key to `Features/Localization/Core/Resources/en.json` first — English declares the key set
every other file mirrors — then to **every other resource file in that directory**, translated.
`Tests/Features/Localization/Core/LocalizationResourceStoreTests` (run by `dotnet test`) fails when:

- `en` is not shipped, or a shipped file has no `LanguageCatalog` entry;
- any file is missing a key English has, or has one English lacks;
- a value is empty, or is the key itself.

On the web, `i18n/tenant-screens.test.ts` (run by `npm run test`) reads the same resource files and
fails when a tenancy error code or tenancy screen key it lists is undefined in any culture, or a
message that names a workspace drops its `${name}` or `${host}` placeholder. Placeholders are not checked in general, so
keep every `${…}` of the English value in each translation yourself.

Follow the established namespaces:

`brand.*`, `common.*`, `cookieConsent.*`, `navigation.*`, `search.*`, `page.<area>.*` (or
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
`Localization.View` / `Localization.Update`, `PermissionScope.Both`, no feature gate). Each edit
writes to the acting scope only — a platform account in no tenant edits the platform rows every tenant
inherits, a tenant administrator edits that tenant's:

| Endpoint | Does |
| --- | --- |
| `GET /localization/texts` | One page of keys for a culture: shipped, inherited and own value |
| `PUT` / `DELETE /localization/texts` | Set or remove this scope's override of one key |
| `GET /localization/languages` | Shipped languages, this scope's enabled set and default, and what it inherits |
| `PUT` / `DELETE /localization/languages` | Set or remove this scope's `LanguageSetting` row |

Overrides are keyed by the shipped key set: a key that is not in `en.json` cannot be edited, so new
copy always starts as a resource-file entry. After a save the screen calls `router.refresh()` so the
root layout re-fetches the dictionary; so does anything that changes the acting scope (sign-in, tenant
switch or exit).

## Routing

Routable locales are `i18nConfig.locales` in `i18n/config.ts` (`defaultLocale: 'en'`). The default
locale's URLs carry no prefix — `proxy.ts` rewrites internally for it and redirects for the others,
so `/admin/users` and `/ar/admin/users` are both valid while `/en/admin/users`
redirects to the unprefixed form. For a URL with no locale, `i18n/resolve-locale.ts` picks, first
answer wins: the `preferred-language` cookie (written by `LanguageDropdown`) → the `scope-language`
cookie → `Accept-Language` → the default, counting only cultures in the `scope-languages` cookie
(the acting scope's enabled set). A URL whose locale is routable but not enabled is redirected to the
resolved one. The whole decision is the pure `decideLocaleRouting` in `i18n/locale-routing.ts`, which
`proxy.ts` only wraps.

`LocaleGuard` (`components/layouts/locale-guard.tsx`, decision in `i18n/locale-guard.ts`) runs inside
the provider: when the served culture differs from the URL's locale (not enabled for this scope) it
navigates to the served one; when the visitor has no `preferred-language` cookie and the scope has a
default that differs, it navigates to that default. It only ever targets a culture that is enabled and routable,
keeps the `scope-language` (default) and `scope-languages` (enabled set) cookies in step, and clears a
`preferred-language` cookie naming a culture the scope does not enable — which is what keeps the proxy
and the API agreeing. `i18n/locale-routing-loop.test.ts` replays proxy → API → guard hops over a matrix
of scopes and visitors and fails when any combination does not settle; extend it when the rule changes. Links and navigation stay locale-aware through `LocalizedLink` and
`useLocalizedRouter()`.

The language dropdown lists `languages` from the dictionary, so a scope that enables a subset offers
only that subset.

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
enable the removed code are not rewritten, so re-save the affected scopes' languages from the admin
screen (the update endpoint accepts only shipped cultures).

## RTL

The served language's `isRtl` (from `LanguageCatalog`, delivered in `languages`) drives
`themeConfigSlice.rtlClass`; `App.tsx` dispatches it and writes `dir` on `<html>`, so serving Arabic or
Urdu flips the document direction. Consequently **all component styling must be direction-agnostic**:
use `ms-`/`me-`, `ps-`/`pe-`, `inset-s-`/`inset-e-`, `text-start`/`text-end`, `border-s`/`border-e`,
`rounded-s`/`rounded-e` rather than `ml-`/`pl-`/`left-`/`text-left`, or pair `ltr:`/`rtl:` variants
where no logical utility exists. Components that must branch read
`useAppSelector((state) => state.theme.rtlClass) === 'rtl'` — the anchor side of
`DataTableExportButton` and `DataTableRowActions` is the existing example. `i18n/tenant-screens.test.ts`
also scans the tenancy, tenant-selection, localization and settings screens for physical utilities;
extend its directory list when you want a new feature's screens held to the same rule.

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
- [ ] `dotnet test` (resource-file test) and `npm run test` (locale and tenant-screen tests) pass
