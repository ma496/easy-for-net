# Add the Features/Settings slice with platform and tenant overrides, and move Signin onto it

| | |
|---|---|
| **Commit** | `e0b27add` |
| **Landed** | 2026-09-30 |
| **Task brief** | `01-settings-slice-and-signin-setting.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/doing/01-settings-slice-and-signin-setting.md` | 73 | 0 |
| `.agent-queue/planned.json` | 2 | 1 |
| `.agent-queue/todo/02-email-setting-with-configuration-default-and-secrets.md` | 52 | 0 |
| `.agent-queue/todo/03-admin-settings-page.md` | 38 | 0 |
| `.agent-queue/todo/04-document-settings-system.md` | 29 | 0 |
| `.claude/memory/lessons/clear-next-cache-when-next-build-fails-on-next-font-google.md` | 9 | 0 |
| `src/backend/Source/ErrorHandling/ErrorCodes.cs` | 4 | 0 |
| `src/backend/Source/Extensions/EndpointExtension.cs` | 19 | 0 |
| `src/backend/Source/Features/Identity/Core/IdentitySettingsProvider.cs` | 17 | 0 |
| `src/backend/Source/Features/Identity/Core/SigninSetting.cs` | 0 | 9 |
| `src/backend/Source/Features/Identity/Core/SigninSettings.cs` | 23 | 0 |
| `src/backend/Source/Features/Identity/Core/TenantAuthorizationService.cs` | 17 | 2 |
| `src/backend/Source/Features/Identity/Endpoints/Account/ResendVerifyEmailEndpoint.cs` | 5 | 2 |
| `src/backend/Source/Features/Identity/Endpoints/Account/SignupEndpoint.cs` | 6 | 2 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenEndpoint.cs` | 9 | 4 |
| `src/backend/Source/Features/Identity/Endpoints/Account/TokenService.cs` | 11 | 6 |
| `src/backend/Source/Features/Identity/IdentityFeature.cs` | 0 | 3 |
| `src/backend/Source/Features/Localization/Core/Resources/ar.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/en.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/es.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/fr.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/hi.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/ru.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/ur.json` | 5 | 1 |
| `src/backend/Source/Features/Localization/Core/Resources/zh.json` | 5 | 1 |
| `src/backend/Source/Features/Settings/Core/Entities/Configuration/SettingValueConfiguration.cs` | 39 | 0 |
| `src/backend/Source/Features/Settings/Core/Entities/SettingValue.cs` | 23 | 0 |
| `src/backend/Source/Features/Settings/Core/ISettingDefinitionProvider.cs` | 22 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingDefinition.cs` | 128 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingDefinitionBuilder.cs` | 23 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingDefinitionCatalogue.cs` | 104 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingDefinitionContext.cs` | 46 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingJson.cs` | 59 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingProvider.cs` | 60 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingValueService.cs` | 161 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingValueStore.cs` | 92 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingsDtos.cs` | 47 | 0 |
| `src/backend/Source/Features/Settings/Core/SettingsPermissionsProvider.cs` | 22 | 0 |
| `src/backend/Source/Features/Settings/Endpoints/Settings/SettingDeleteEndpoint.cs` | 47 | 0 |
| `src/backend/Source/Features/Settings/Endpoints/Settings/SettingListEndpoint.cs` | 41 | 0 |
| `src/backend/Source/Features/Settings/Endpoints/Settings/SettingUpdateEndpoint.cs` | 144 | 0 |
| `src/backend/Source/Features/Settings/Endpoints/Settings/SettingsGroup.cs` | 12 | 0 |
| `src/backend/Source/Features/Settings/SettingsFeature.cs` | 35 | 0 |
| `src/backend/Source/Migrations/20260929232356_AddSettingValues.Designer.cs` | 944 | 0 |
| `src/backend/Source/Migrations/20260929232356_AddSettingValues.cs` | 53 | 0 |
| `src/backend/Source/Migrations/AppDbContextModelSnapshot.cs` | 41 | 0 |
| `src/backend/Source/Permissions/Allow.cs` | 3 | 0 |
| `src/backend/Source/ShareData/AppDbContext.cs` | 4 | 0 |
| `src/backend/Source/appsettings.json` | 0 | 3 |
| `src/backend/Tests/Fakes/PlatformSettingOverlays.cs` | 62 | 0 |
| `src/backend/Tests/Fakes/ProbeSettings.cs` | 42 | 0 |
| `src/backend/Tests/Fakes/TestDoubles.cs` | 22 | 1 |
| `src/backend/Tests/Features/Settings/Core/SettingDefinitionCatalogueTests.cs` | 132 | 0 |
| `src/backend/Tests/Features/Settings/Core/SettingProviderTests.cs` | 157 | 0 |
| `src/backend/Tests/Features/Settings/Endpoints/Settings/SettingDeleteTests.cs` | 104 | 0 |
| `src/backend/Tests/Features/Settings/Endpoints/Settings/SettingListTests.cs` | 87 | 0 |
| `src/backend/Tests/Features/Settings/Endpoints/Settings/SettingUpdateTests.cs` | 208 | 0 |
| `src/backend/Tests/Features/Settings/SettingsTestsBase.cs` | 109 | 0 |
| `src/backend/Tests/Features/Settings/SigninSettingsTests.cs` | 191 | 0 |
| `src/frontend/web/allow.test.ts` | 22 | 0 |
| `src/frontend/web/allow.ts` | 3 | 0 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

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
