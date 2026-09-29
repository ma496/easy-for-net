Add a typed settings system with platform and tenant overrides, starting with sign-in and email

Today `Signin` and `EmailSettings` are `appsettings.json` sections bound to `SigninSetting` and
`EmailSetting` through `IOptions`. Changing either needs a redeploy, and neither can differ between
tenants, so every tenant signs in under the same verification rule and sends mail through the same
SMTP account.

After this change a **setting** is a C# class whose properties are its values — any type the JSON
serializer handles (bool, int, string, enum, nested object, list). Every read is typed:
`GetAsync<SigninSettings>()`, never a string key and a cast. A value resolves through three layers,
property by property, first answer winning:

1. the **tenant override** — set by a tenant administrator, for their own tenant;
2. the **platform override** — set by a platform administrator, for every tenant that has not
   overridden that property, and for everything that runs in no tenant;
3. the **default** — declared per setting as coming from either:
   - **code** — the class's property initializers; nothing reads configuration; or
   - **configuration** — the property initializers overlaid by a named configuration section, so a
     deployment sets it in `appsettings.json`, an environment's appsettings file or environment
     variables, exactly as today.

A property no layer overrides follows the one below it, so removing an override never needs the
administrator to know what the value underneath was.

## Scope

### The settings system — a new `Features/Settings` slice

- **Definitions.** Each slice declares its settings in `Core/<X>SettingsProvider.cs`
  (`ISettingDefinitionProvider`, discovered by assembly scan like the permission and feature
  providers), registering a type under a name and choosing where its default comes from:
  `context.Add<SigninSettings>("Signin")` (code) or
  `context.Add<EmailSettings>("Email").FromConfiguration("EmailSettings")` (configuration). A name is
  unique, and so is a type; a duplicate fails startup. Each setting type has a FluentValidation
  validator registered with its definition; every default is validated at startup and an invalid one
  fails startup, as `ValidateOnStart` does today. A configuration section that is missing leaves the
  code defaults in force.
- **Storage.** One table, `SettingValue` — `IMayHaveTenant` (`TenantId == null` is the platform
  override, as with `LocalizationText` and `LanguageSetting`), the setting name, and a `jsonb` column
  holding only the properties that scope overrode. One row per scope and name, platform included:
  the unique index must treat `null` tenants as equal (`NULLS NOT DISTINCT`, or a partial index for
  the platform rows). Tenant isolation comes from the `AppDbContext` tenant filter as for every other
  tenant-optional entity; the resolver reads the platform row deliberately, never by lifting the
  filter wholesale.
- **Resolution.** `ISettingProvider.GetAsync<T>()` resolves for the acting scope from
  `ITenantContext`; `GetAsync<T>(Guid? tenantId)` takes the target explicitly, for code that runs
  outside a request or before a tenant scope exists (sign-in, background jobs). The result is a new
  instance of `T`: the default, with the platform row laid over it, with the tenant's row laid over
  that. No tenant (platform scope, anonymous, `null`) means default + platform override. A stored
  property the type no longer has is ignored; a property the type gained since follows the layer
  below. Values are read per call and cached for the request only — a saved override is in force on
  the next request.
- **Secrets.** A property marked `[SecretSetting]` is encrypted at rest with ASP.NET Data Protection
  and never leaves the API — whichever layer it comes from, the configured default included: reads
  return it as `null` beside an `isSet` flag, and a write that sends `null` keeps the stored value (an
  explicit clear removes the override). A stored secret that cannot be decrypted is logged and treated
  as not overridden at that layer, never thrown to the caller.
- **Published vocabulary.** `ISettingProvider`, `ISettingDefinitionProvider`, the definition context
  and `[SecretSetting]` are `[AllowOutside]`, as `IFeatureChecker` is; the store, the entity and the
  merge stay private to the slice.
- **Endpoints**, all under `/settings`, all acting on the **acting scope's own** overrides — the
  tenant's row inside a tenant, the platform row in no tenant — the same way the `/localization`
  texts and languages endpoints do:
  - `GET /settings` — every definition: name, effective values, and for each property which layer
    it comes from (`tenant`, `platform`, `default`), secrets masked. `Settings.View`.
  - `PUT /settings/{name}` — the properties this scope overrides, validated by that setting's
    validator against the merged result; an unknown name is 404, an unknown property 400.
    `Settings.Update`.
  - `DELETE /settings/{name}` — removes this scope's override: a tenant falls back to the platform
    value, the platform to the default. `Settings.Update`.
  - `Settings.View` and `Settings.Update` are new permissions with `PermissionScope.Both` under a
    Settings group, gated on no feature, mirrored in `allow.ts`. A tenant administrator never reads or
    writes the platform row, and a platform administrator edits a tenant's overrides only by entering
    that tenant as a member, as for every other tenant-scoped change.
  - Every refusal is a coded error (`settingNotFound`, `settingPropertyUnknown`, …) shipped in every
    resource file.

### The two settings

- **`Signin`** — default from **code**. `SigninSettings { IsEmailVerificationRequired = false }` in
  `Features/Identity/Core`, replacing `SigninSetting`. The `Signin` section is removed from
  `appsettings.json` and `appsettings.Development.json`, with the `AddOptions<SigninSetting>` binding
  in `IdentityFeature`.
  - Sign-in (`TokenEndpoint`) checks it for the tenant being entered, refresh (`TokenService`) for the
    session's tenant, signup and `ResendVerifyEmailEndpoint` for no tenant (default + platform — the
    tenant a signup creates has no overrides yet). A platform account signing in with no tenant is
    checked against default + platform.
- **`Email`** — default from **configuration**, the existing `EmailSettings` section, which stays in
  `appsettings.json` and the related files unchanged. `EmailSettings` in `External/Email` replaces
  `EmailSetting`, with the same six properties; `SmtpPassword` is `[SecretSetting]`. The validator
  requires a server, a port in `1..65535` and a valid sender address. The `Configure<EmailSetting>`
  line in `Program.cs` goes; `IOptions<EmailSetting>` is no longer read.
  - `EmailService` resolves the setting at send time. `EmailBackgroundJobs` captures the acting tenant
    id when it enqueues and passes it to the job, which resolves with
    `GetAsync<EmailSettings>(tenantId)` — a Hangfire job has no request and no tenant scope (see the
    `background-jobs` skill). Email enqueued with no tenant uses default + platform.

### Web

- An **admin settings page**, `app/[lang]/admin/settings`, behind `Settings.View`, registered in
  `nav-items.ts`, `searchable-items.ts` and `auth-urls.ts`, reachable in both tenant and platform
  scope and editing that scope's overrides: one card per setting (Sign-in, Email) with a typed Formik
  + Yup form, a marker per property saying where its value comes from (this scope, platform,
  default), Save (hidden without `Settings.Update`) and Reset. The SMTP password field shows "set" /
  "not set", is never pre-filled, and is only sent when typed into.
- **Typed DTOs** in `store/api/settings/` mirroring each setting class by name (`SigninSettingsDto`,
  `EmailSettingsDto`) — no `Record<string, unknown>` in component code. The settings cache is part of
  what the tenant-change reset clears.
- Every new string is a translation key in every resource file.

### Docs and skills

- A `settings` skill in `.claude/skills/` (adding a setting, code or configuration as its default,
  the three layers, secrets, resolving outside a request), a **Settings** paragraph in `CLAUDE.md`
  beside Features, and the `background-jobs` skill updated where it describes how email is configured.

## Out of scope — do not touch

- Password rules: the validators keep their hard-coded rules; there is no password setting.
- Other configuration — `Auth`, `Payload`, `Web`, `Redis`, `Hangfire`, `ConnectionStrings`,
  `FeatureManagement` — stays as it is, read through options at startup.
- A platform administrator editing a tenant's overrides from platform scope, or locking a property so
  tenants cannot override it.
- Per-user settings.
- Feature management and entitlements: a setting is not a feature and is not gated on one.
- Session revocation: a settings change revokes nothing. `IsEmailVerificationRequired` reaches an
  existing session at its next refresh.

## Done when

- `npm run verify` passes, and `dotnet ef migrations add` has produced the migration for `SettingValue`.
- Integration tests cover:
  - with no overrides, `Signin` resolves to its code default and `Email` to the configuration section;
  - a platform override changes the value for platform scope and for every tenant without its own
    override of that property; a tenant override wins over it for that tenant only, and properties
    the tenant did not override follow the platform value;
  - `DELETE` from a tenant falls back to the platform value, and from platform scope to the default;
  - a tenant administrator cannot read or change the platform row; an invalid value (port `0`, a
    malformed sender address) is 400 with its code; an unknown name is 404; a caller without
    `Settings.Update` gets 403;
  - the SMTP password — from any layer — is never in any response, is not stored in plain text in the
    row, and a `PUT` without it keeps the stored one;
  - `IsEmailVerificationRequired` overridden to `true` at platform level refuses sign-in of an
    unverified member into any tenant that has not overridden it back to `false`;
  - an email enqueued in a tenant resolves that tenant's `EmailSettings` when the job runs, and one
    enqueued with no tenant resolves default + platform.
  - Tests that write the platform row share a `[Collection]`, since that row is global state.
- On the web: the settings page loads, saves, resets and shows where each value comes from, in both
  tenant and platform scope; a user without `Settings.Update` sees it read-only.
