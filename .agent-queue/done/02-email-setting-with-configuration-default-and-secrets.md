Move EmailSettings onto the settings system, with configuration defaults and encrypted secrets
Depends-on: 01-settings-slice-and-signin-setting

Task 01 built the settings system with code defaults and moved `Signin` onto it. This task adds the two remaining capabilities of that system:

- a default read from configuration;
- secret properties.

It then moves `EmailSettings` onto the system, so that each tenant can send mail through its own SMTP account while a deployment still sets the default in `appsettings.json` or environment variables, as it does today.

## Scope
- **Configuration defaults.**
  - A definition can declare `.FromConfiguration("Section")`. Its default is then the property initializers overlaid by that configuration section.
  - A missing section leaves the code defaults in force.
  - The configured default is validated at startup like every other default.
- **`[SecretSetting]`**, published as `[AllowOutside]`, marks a secret property.
  - A secret property is encrypted at rest with ASP.NET Data Protection.
  - It never leaves the API, whichever layer it comes from, the configured default included. `GET /settings` returns it as `null` beside an `isSet` flag.
  - A `PUT` that sends `null` keeps the stored value. An explicit clear removes the override.
  - A stored secret that cannot be decrypted is logged and treated as not overridden at that layer. It is never thrown to the caller.
- **`EmailSettings`.**
  - Add `EmailSettings` in `External/Email`, replacing `EmailSetting`, with the same six properties. `SmtpPassword` is `[SecretSetting]`.
  - Declare it with `.FromConfiguration("EmailSettings")`. The `EmailSettings` section in the appsettings files stays unchanged.
  - Its validator requires a server, a port in `1..65535`, and a valid sender address. Each rule carries an error code, and each code has a message in every resource file.
  - Remove `Configure<EmailSetting>` from `Program.cs`. Nothing reads `IOptions<EmailSetting>` any more.
- **Sending email.**
  - `EmailService` resolves the setting when it sends.
  - `EmailBackgroundJobs.Enqueue` captures the acting tenant id and passes it into the job.
  - The job resolves the setting with `GetAsync<EmailSettings>(tenantId)`, following the `background-jobs` skill: a job has no request and no tenant scope.
  - An email enqueued with no tenant resolves the default plus the platform row.
- **Integration tests**, in the `[Collection("Settings")]` from task 01 when they write the platform row. The tests cover:
  - with no overrides, `Email` resolves to the configuration section;
  - an invalid port (`0`) or a malformed sender address is 400 with its code;
  - the SMTP password, whichever layer sets it, is never in any response;
  - the SMTP password is not stored in plain text in the row;
  - a `PUT` without the SMTP password keeps the stored one;
  - an email enqueued in a tenant resolves that tenant's `EmailSettings` when the job runs;
  - an email enqueued with no tenant resolves the default plus the platform row.

## Out of scope — do not touch
- The layering, storage, endpoints and permissions from task 01, beyond the additions above, and `SigninSettings` together with its callers.
- The web app, including `store/api/settings/` and the settings page, which belong to task 03.
- `CLAUDE.md` and `.claude/skills/background-jobs/SKILL.md`, which belong to task 04.
- Every other options section. Password rules. Session revocation.

## Done when
- `npm run verify` passes.
- Every test listed under Scope exists and passes.
- `FeatureDependencyTests` passes.
- `LocalizationResourceStoreTests` passes.
- With the database untouched, mail still goes out through the `EmailSettings` section exactly as before.
- Reading the row directly shows no plain-text password.
