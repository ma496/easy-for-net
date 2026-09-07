---
name: localization
description: Work with i18n in the web app — adding translation keys, the three translator entry points (client hook, server helper, non-component), adding or removing a locale, RTL handling, and how locale-prefixed routing works. Use when adding user-facing text or a new language.
---

# Localization

Locales are declared in `i18n/config.ts` and messages live in `public/locales/<code>.json`.
The default locale is `en`, and its URLs carry no prefix — `proxy.ts` rewrites internally for the
default locale and redirects for the others, so `/admin/users/list` and `/ar/admin/users/list` are
both valid while `/en/admin/users/list` redirects to the unprefixed form.

## Three ways to translate

| Where | Use | Import |
| --- | --- | --- |
| Client component | `const { t } = useTranslation()` then `t('page.users.title')` | `@/i18n` |
| Server component | `await getServerTranslation(lang, 'page.users.title')` | `@/i18n` |
| Non-component module (alerts, utils) | `const { t } = getTranslation()` | `@/i18n` |

`getTranslation` reads the dictionary that `TranslationProvider` published globally, so it only
works client-side after the provider mounted and does **not** react to a language change without a
reload. Prefer passing already-translated text into helpers where you can.

All three resolve dot-notation keys and return **the key itself** when it is missing — which is why
`isTranslationKeyExist(key)` exists for optional copy, and why a raw key showing in the UI means a
missing entry rather than a crash.

Interpolation is `${name}` in the JSON value:

```json
"validation": { "minLength": "Must be at least ${min} characters" }
```

```tsx
t('validation.minLength', { min: 3 })
```

## Adding keys

Add to `public/locales/en.json` first, then to **every other locale file present in the project** —
a key that exists only in English renders as the raw key for other languages. Follow the established
namespaces:

`common.*`, `navigation.*`, `search.*`, `page.<area>.*`, `form.label.*`, `form.placeholder.*`,
`validation.*`, `table.columns.*` / `table.filter.*` / `table.export.*` / `table.actions`,
`error.400.title` and `error.server.<errorCode>`, `notifications.*`, `brand.name`.

Two namespaces are contracts with the backend, not free-form copy:

- `error.server.<code>` must match a constant in `ErrorHandling/ErrorCodes.cs` — see the
  `api-error-handling` skill.
- `notifications.<name>.title` / `.message` must match the `TitleKey`/`MessageKey` strings a
  notification is created with — see the `notifications` skill.

Never hard-code user-facing strings in components; the only literals in JSX should be keys.

## Adding a locale

1. Add the code to `i18nConfig.locales` in `i18n/config.ts`.
2. Add `public/locales/<code>.json` — start from `en.json` so every key exists.
3. Register the dynamic import in the `dictionaries` map in `i18n/server.ts`.
4. Add `{ code, name, isRTL }` to `languageList` in `store/slices/themeConfigSlice.tsx`, and a flag
   image under `public/assets` if the language dropdown expects one.

Removing a locale is the same four places in reverse.

## RTL

`themeConfigSlice` tracks `rtlClass` and each language's `isRTL` flag; switching to Arabic or Urdu
flips the document direction. Consequently **all component styling must be direction-agnostic**:
use `ms-`/`me-`, `ps-`/`pe-`, `inset-s-`/`inset-e-` rather than `ml-`/`pl-`/`left-`. Components that
must branch read `useAppSelector((state) => state.theme.rtlClass) === 'rtl'` — the export dropdown's
placement is the existing example.

## Single-language projects

`dotnet efn cp` defaults to `-m false`, which strips the non-English locale files and reduces
`i18n/config.ts`, `i18n/server.ts` and `themeConfigSlice.tsx` to `en` alone. In such a project there
is exactly one locale file to update — but keep using `t()` and keys anyway, so adding a language
later is only the four steps above.

Because the generator rewrites those three files with regular expressions, keep their shapes intact:
`locales: ['en', …]` as a single-line array in `config.ts`, the `const dictionaries = { … }` object
literal in `server.ts`, and `languageList: [ … ]` in `themeConfigSlice.tsx`.

## Checklist

- [ ] Every new string is a key, added to all shipped locale files
- [ ] Key placed in the right namespace; backend-coupled keys match their constants
- [ ] Server components use `getServerTranslation`, client components `useTranslation`
- [ ] New markup uses logical (RTL-safe) spacing utilities
- [ ] A new locale is registered in all four places
