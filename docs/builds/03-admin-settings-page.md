# Add the admin settings page for editing sign-in and email overrides in the acting scope

| | |
|---|---|
| **Commit** | `0c61bb5e` |
| **Landed** | 2026-09-30 |
| **Task brief** | `03-admin-settings-page.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/03-admin-settings-page.md` | 0 | 0 |
| `.agent-queue/{doing => done}/02-email-setting-with-configuration-default-and-secrets.md` | 0 | 0 |
| `docs/builds/02-email-setting-with-configuration-default-and-secrets.md` | 113 | 0 |
| `docs/builds/README.md` | 2 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/ar.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/en.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/es.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/fr.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/hi.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/ru.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/ur.json` | 55 | 2 |
| `src/backend/Source/Features/Localization/Core/Resources/zh.json` | 55 | 2 |
| `src/frontend/web/app/[lang]/admin/settings/_components/email-settings-card.tsx` | 159 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/_components/settings-card-parts.tsx` | 141 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/_components/settings-form.test.ts` | 148 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/_components/settings-form.ts` | 145 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/_components/settings-manager.tsx` | 103 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/_components/signin-settings-card.tsx` | 84 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/_components/use-setting-reset.ts` | 38 | 0 |
| `src/frontend/web/app/[lang]/admin/settings/page.tsx` | 27 | 0 |
| `src/frontend/web/auth-urls.test.ts` | 2 | 0 |
| `src/frontend/web/auth-urls.ts` | 6 | 0 |
| `src/frontend/web/i18n/tenant-screens.test.ts` | 1 | 1 |
| `src/frontend/web/lib/utils/tenant-routing.test.ts` | 10 | 0 |
| `src/frontend/web/lib/utils/tenant-routing.ts` | 3 | 2 |
| `src/frontend/web/nav-items.ts` | 6 | 1 |
| `src/frontend/web/searchable-items.ts` | 4 | 0 |
| `src/frontend/web/store/api/settings/index.ts` | 16 | 0 |
| `src/frontend/web/store/api/settings/settings/settings-api.ts` | 30 | 0 |
| `src/frontend/web/store/api/settings/settings/settings-dtos.ts` | 92 | 0 |
| `src/frontend/web/store/api/settings/settings/settings-mappers.test.ts` | 72 | 0 |
| `src/frontend/web/store/api/settings/settings/settings-mappers.ts` | 50 | 0 |
| `src/frontend/web/store/tenant-cache.test.ts` | 31 | 0 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Depends-on: 01-settings-slice-and-signin-setting, 02-email-setting-with-configuration-default-and-secrets

Tasks 01 and 02 put `Signin` and `Email` behind `/settings`. This task gives administrators a screen to edit them. The page edits the acting scope's own overrides: a tenant's own overrides inside a tenant, and the platform's in platform scope. For each property it shows where the value comes from.

## Scope
- **API slice.** Add `store/api/settings/` using `injectEndpoints` on `appApi`. It covers `GET /settings`, `PUT /settings/{name}` and `DELETE /settings/{name}`, with cache tags.
  - Typed DTOs mirror each setting class by name: `SigninSettingsDto` and `EmailSettingsDto`, together with the per-property source and the secret `isSet` flag.
  - Component code never uses `Record<string, unknown>`.
  - The settings cache is part of what the tenant-change reset clears.
- **The page.** Add `app/[lang]/admin/settings` behind `Settings.View`.
  - Register it in `nav-items.ts`, `searchable-items.ts` and `auth-urls.ts`.
  - It is reachable in both tenant and platform scope.
- **The cards.** One card per setting, Sign-in and Email, each with a typed Formik + Yup form.
  - Each property has a marker saying where its value comes from: this scope, platform or default.
  - Save is hidden without `Settings.Update`, and the form is then read-only.
  - Reset calls `DELETE`.
  - API errors are shown through the existing error helpers.
- **The SMTP password field.**
  - It shows "set" or "not set" and is never pre-filled.
  - It is sent only when the user types into it.
- **Translations.** Every new string is a translation key in every backend resource file.
- **Vitest tests** for any pure logic the page adds. Examples: building the `PUT` body so an untouched password is omitted, and mapping the property sources. Also extend the tenant-change reset test to cover the settings tags.

## Out of scope — do not touch
- The backend settings slice, its endpoints, `SigninSettings` and `EmailSettings`, all owned by tasks 01 and 02. The only backend change here is new keys in the resource files.
- The `Settings.*` entries in `allow.ts`, which task 01 added. Use them as they are.
- `CLAUDE.md` and `.claude/skills/`, which belong to task 04.
- The other admin screens, including `admin/localization`.

## Done when
- `npm run verify` passes, including lint, `tsc --noEmit` and vitest.
- In tenant scope and in platform scope, the page loads, saves, resets, and shows where each value comes from.
- A tenant's save does not change what platform scope shows.
- A user without `Settings.Update` sees the page read-only.
- The SMTP password is never shown or pre-filled.
- Switching tenant refetches the settings instead of showing the previous tenant's values.
- The page works in dark mode and in right-to-left layout.
