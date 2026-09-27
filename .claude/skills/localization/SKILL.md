---
name: localization
description: Work with i18n in the web app — adding translation keys, the three translator entry points (client hook, server helper, non-component), the locale-file test that keeps every locale in step, adding or removing a locale, RTL handling, and how locale-prefixed routing works. Use when adding user-facing text or a new language.
---

# Localization

Locales are declared in `i18n/config.ts` (`defaultLocale: 'en'`, `locales: [...]`) and messages live
in `public/locales/<code>.json`. The default locale's URLs carry no prefix — `proxy.ts` rewrites
internally for the default locale and redirects for the others, so `/admin/users/list` and
`/ar/admin/users/list` are both valid while `/en/admin/users/list` redirects to the unprefixed form.
Links and navigation stay locale-aware through `LocalizedLink` and `useLocalizedRouter()`.

## Three ways to translate

| Where | Use | Import |
| --- | --- | --- |
| Client component | `const { t, i18n } = useTranslation()` then `t('page.users.title')` | `@/i18n` |
| Server component | `await getServerTranslation(lang, 'page.users.title')` | `@/i18n` |
| Non-component module (alerts, utils) | `const { t } = getTranslation()` | `@/i18n` |

`useTranslation` also returns `i18n.language` (the locale from the URL) and `i18n.changeLanguage(code)`.
`getTranslation` reads the dictionary that `TranslationProvider` published globally, so it only
works client-side after the provider mounted and does **not** react to a language change without a
reload. Prefer passing already-translated text into helpers where you can.

All three resolve dot-notation keys and return **the key itself** when it is missing — which is why
`isTranslationKeyExist(key)` (`@/lib/utils`) exists for optional copy, and why a raw key showing in
the UI means a missing entry rather than a crash.

Interpolation is `${name}` in the JSON value:

```json
"validation": { "minLength": "Must be at least ${min} characters" }
```

```tsx
t('validation.minLength', { min: 3 })
t('page.tenants.switcher.switchSuccess', { tenant: name })
```

## Adding keys

Add to `public/locales/en.json` first, then to **every other locale file present in the project**,
translated. `i18n/locales.test.ts` (run by `npm run test`) fails when:

- a configured locale has no file, or a file exists for an unconfigured one;
- any locale is missing a key another has, or has one the others lack;
- a message is empty, or is the key itself;
- a tenancy error code or tenancy screen key it lists is undefined, or the tenant-switch message
  drops its `${tenant}` placeholder.

Placeholders are not checked in general, so keep every `${…}` of the English value in each
translation yourself.

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

Never hard-code user-facing strings in components; the only literals in JSX should be keys.

## Adding a locale

1. Add the code to `i18nConfig.locales` in `i18n/config.ts`.
2. Add `public/locales/<code>.json` — start from `en.json` so every key exists, then translate every
   value (the locale test rejects a value equal to its key, not one equal to the English text, so
   review it).
3. Register the dynamic import in the `dictionaries` map in `i18n/server.ts`.
4. Add `{ code, name, isRTL }` to `languageList` in `store/slices/themeConfigSlice.tsx`, and make sure
   `public/assets/images/flags/<CODE>.svg` exists — `LanguageDropdown` loads the flag by the
   upper-cased locale code.

Removing a locale is the same four places in reverse.

## RTL

`themeConfigSlice` tracks `rtlClass`; `App.tsx` sets it from the current language's `isRTL` flag and
writes `dir` on `<html>`, so switching to Arabic or Urdu flips the document direction. Consequently
**all component styling must be direction-agnostic**: use `ms-`/`me-`, `ps-`/`pe-`,
`inset-s-`/`inset-e-`, `text-start`/`text-end`, `border-s`/`border-e`, `rounded-s`/`rounded-e` rather
than `ml-`/`pl-`/`left-`/`text-left`, or pair `ltr:`/`rtl:` variants where no logical utility exists.
Components that must branch read `useAppSelector((state) => state.theme.rtlClass) === 'rtl'` — the
anchor side of `DataTableExportButton` and `DataTableRowActions` is the existing example. The locale
test also scans the tenancy screens for physical utilities; extend its directory list when you want a
new feature's screens held to the same rule.

## Single-language projects

`dotnet efn cp` defaults to `-m false`, which deletes the non-English locale files and reduces
`i18n/config.ts`, `i18n/server.ts` and `themeConfigSlice.tsx` to `en` alone. In such a project there
is exactly one locale file to update — but keep using `t()` and keys anyway, so adding a language
later is only the four steps above.

Because the generator rewrites those three files with regular expressions, keep their shapes intact:
`locales: ['en', …]` as a single-line array in `config.ts`, the `const dictionaries = { … }` object
literal in `server.ts`, and `languageList: [ … ]` in `themeConfigSlice.tsx`.

## Checklist

- [ ] Every new string is a key, added and translated in all shipped locale files
- [ ] Key placed in the right namespace; backend-coupled keys match their constants
- [ ] Server components use `getServerTranslation`, client components `useTranslation`
- [ ] New markup uses logical (RTL-safe) utilities
- [ ] A new locale is registered in all four places, flag included
- [ ] `npm run test` passes (the locale-file test)
