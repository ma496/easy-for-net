Add the Features/Settings slice with platform and tenant overrides, and move Signin onto it

Today `Signin` is an `appsettings.json` section bound to `SigninSetting` through `IOptions`, so changing it needs a redeploy and every tenant signs in under the same verification rule. This task builds the typed settings system in a new `Features/Settings` slice and moves `Signin` onto it. A setting is a C# class whose properties are its values. Any type the JSON serializer handles is allowed. Every read is typed: `GetAsync<SigninSettings>()`. A value resolves property by property, and the first layer with an answer wins:

1. the tenant override;
2. the platform override;
3. the code default, which is the class's property initializers.

A property that no layer overrides follows the layer below it. Defaults read from configuration and `[SecretSetting]` are task 02's; this task builds only code defaults, and `Signin` needs nothing more.

## Scope
- **Definitions.**
  - Each slice declares its settings in `Core/<X>SettingsProvider.cs`, implementing `ISettingDefinitionProvider`.
  - Providers are discovered by assembly scan, as the permission and feature providers are.
  - A provider registers a setting with `context.Add<T>("Name")`, together with the setting type's FluentValidation validator.
  - A duplicate name or a duplicate type fails startup.
  - Every default is validated at startup, and an invalid default fails startup.
  - Design the definition so that task 02 can add `.FromConfiguration("Section")` and secret properties without reshaping it.
- **Storage.**
  - A `SettingValue` entity, `IMayHaveTenant`. `TenantId == null` is the platform row, as with `LocalizationText` and `LanguageSetting`.
  - Columns: the setting name, and a `jsonb` column that holds only the properties that scope overrode.
  - A unique index on (scope, name) that treats `null` tenants as equal: `NULLS NOT DISTINCT`, or a partial index for the platform rows.
  - Tenant isolation comes from the `AppDbContext` tenant filter. The resolver reads the platform row deliberately, never by lifting the filter wholesale.
  - Add the migration with `dotnet ef migrations add`.
- **Resolution.**
  - `ISettingProvider.GetAsync<T>()` resolves for the acting scope, read from `ITenantContext`.
  - `GetAsync<T>(Guid? tenantId)` takes the target tenant explicitly.
  - The result is a new `T`: the default, with the platform row laid over it, and the tenant row laid over that. With no tenant, the result is the default plus the platform row.
  - A stored property that the type no longer has is ignored.
  - Values are read per call and cached for the current request only.
- **Published vocabulary.**
  - `ISettingProvider`, `ISettingDefinitionProvider` and the definition context are `[AllowOutside]`.
  - The store, the entity and the merge stay private to the slice.
- **Endpoints under `/settings`.** Each one acts on the acting scope's own row: the tenant row inside a tenant, the platform row in no tenant. This is how the `/localization` texts and languages endpoints behave.
  - `GET /settings` returns every definition with its name and effective values. For each property it reports the layer the value comes from: `tenant`, `platform` or `default`. It requires `Settings.View`.
  - `PUT /settings/{name}` saves the properties this scope overrides. The setting's validator checks the merged result. An unknown name is 404 `settingNotFound`. An unknown property, or a value of the wrong type, is 400 `settingPropertyUnknown` or another coded error. It requires `Settings.Update`.
  - `DELETE /settings/{name}` removes this scope's override. It requires `Settings.Update`.
- **Permissions.** Add `Settings.View` and `Settings.Update` under a Settings group:
  - `PermissionScope.Both`, gated on no feature;
  - constants in `Allow.cs`, definitions in a `SettingsPermissionsProvider`, and mirrored in `src/frontend/web/allow.ts`.
- **Error messages.** Every new error code has an `error.server.<code>` message in every resource file under `Features/Localization/Core/Resources/`.
- **`SigninSettings`.**
  - Add `SigninSettings { IsEmailVerificationRequired = false }` in `Features/Identity/Core`, with its code default, declared in an `IdentitySettingsProvider`.
  - Delete `SigninSetting`, the `AddOptions<SigninSetting>` binding in `IdentityFeature`, and the `Signin` section of `appsettings.json` and `appsettings.Development.json`.
  - Each caller resolves the setting for its own tenant:
    - `TokenEndpoint`: the tenant being entered. A platform account signing in with no tenant gets the default plus the platform row.
    - `TokenService` refresh: the session's tenant.
    - `SignupEndpoint` and `ResendVerifyEmailEndpoint`: no tenant.
- **Integration tests.** Every test that writes the platform row is in one `[Collection("Settings")]`. The tests cover:
  - with no overrides, `Signin` resolves to its code default;
  - a platform override applies in platform scope and in every tenant that has not overridden that property;
  - a tenant override wins over the platform override for that tenant only;
  - a property the tenant did not override follows the platform value;
  - `DELETE` from a tenant falls back to the platform value, and `DELETE` from platform scope falls back to the default;
  - a tenant administrator cannot read or change the platform row;
  - an unknown name is 404 with its code, and an unknown property is 400 with its code;
  - a caller without `Settings.Update` gets 403;
  - with `IsEmailVerificationRequired` overridden to `true` at platform level, an unverified member cannot sign in to a tenant that has not overridden it back to `false`, and can sign in to a tenant that has.

## Out of scope — do not touch
- `EmailSetting`, `EmailService`, `EmailBackgroundJobs`, the `EmailSettings` section, the `Configure<EmailSetting>` line in `Program.cs`, `FromConfiguration` and `[SecretSetting]`. Task 02 owns all of them.
- The web app beyond the `allow.ts` mirror. The settings page, `nav-items.ts`, `searchable-items.ts`, `auth-urls.ts` and `store/api/settings/` belong to task 03.
- `CLAUDE.md` and `.claude/skills/`, which belong to task 04.
- Password rules, the other options sections (`Auth`, `Payload`, `Web`, `Redis`, `Hangfire`, `ConnectionStrings`, `FeatureManagement`), per-user settings, and property locking.
- Feature management: a setting is not a feature and is not gated on one.
- Session revocation: a settings change revokes nothing.

## Done when
- `npm run verify` passes, and the migration for `SettingValue` is committed.
- Every test listed under Scope exists and passes.
- `FeatureDependencyTests` passes: Identity reaches Settings only through the `[AllowOutside]` types.
- `LocalizationResourceStoreTests` passes with the new error keys in every resource file.
- An existing session keeps working after a settings change. Nothing is revoked, and `IsEmailVerificationRequired` reaches the session at its next refresh.
