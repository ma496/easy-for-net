Add the admin settings page for editing sign-in and email overrides in the acting scope
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
