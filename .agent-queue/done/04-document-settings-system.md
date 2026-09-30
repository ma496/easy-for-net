Document the settings system in CLAUDE.md and a settings skill
Depends-on: 02-email-setting-with-configuration-default-and-secrets, 03-admin-settings-page

The settings system built by tasks 01 to 03 has to be discoverable by the next person or agent who adds a setting. This task writes it down where the repo's guidance already lives.

## Scope
- **The `settings` skill.** Add `.claude/skills/settings/SKILL.md`, kept generic because it ships to generated projects. It covers:
  - how to add a setting: the class, the provider in `Core/<X>SettingsProvider.cs`, and the validator;
  - choosing a default from code or from configuration with `.FromConfiguration`;
  - the three layers and how they merge property by property;
  - `[SecretSetting]`;
  - resolving a setting outside a request with `GetAsync<T>(tenantId)`;
  - the `/settings` endpoints and the admin page;
  - how tests share the `Settings` collection.
- **`CLAUDE.md`.**
  - Add a **Settings** paragraph beside **Features (entitlements)** under Backend architecture.
  - Add `settings` to the API skills in the Task guides list.
- **The `background-jobs` skill.** Update the Email section. SMTP now comes from `EmailSettings` through `ISettingProvider`. The configuration section is only the default, and a job resolves the setting for the tenant captured when the email was enqueued.
- **Other skills.** Where `backend-feature`, `multi-tenancy` or `permissions` describe the providers a slice declares, name the settings provider beside them.

## Out of scope — do not touch
- Any code under `src/` or `tool/`.
- The `new-project` and `template-maintenance` skills.
- The agent definitions and hooks.

## Done when
- `npm run verify` passes.
- Every type, method and file the docs name exists under that name in the code.
- The skill contains no reference specific to this repository beyond qualified `Backend.` namespaces.
