---
name: settings
description: Add or change a setting — a typed value an administrator may override at run time (the setting class, its validator, the slice's SettingsProvider, a default from code or from configuration with FromConfiguration, [SecretSetting] properties), read it with ISettingProvider in a request or a job, drop a cache when it changes with ISettingChangedHandler, the /settings endpoints and the admin screen, and the Settings test collection. Use when behaviour should be configurable at run time rather than fixed in code or appsettings.
---

# Adding a setting

A **setting** answers *how does this behave here* — a value an administrator may change at run time,
without a deployment. It is not a permission (*may this caller do it*): changing a setting grants
and withholds nothing, and revokes no session. Reach for one when a value that would otherwise sit
in `appsettings.json` should be changed from the admin area.

The system lives in the `Settings` slice (`Features/Settings`). What other slices use is published
with `[AllowOutside]`: `ISettingDefinitionProvider`, `SettingDefinitionContext`,
`SettingDefinitionBuilder<T>`, `SecretSettingAttribute`, `ISettingProvider` and
`ISettingChangedHandler`, all in `Backend.Features.Settings.Core`. Everything that resolves or stores
a value stays private to the slice.

**Small cross-cutting options go on `GeneralSettings`** (`Backend.Features.Settings.Core`, registered
as `General` by `GeneralSettingsProvider`, `[AllowOutside]`), which already holds the sign-in option
`IsEmailVerificationRequired` and the sign-up options `IsSignupEnabled` and `SignupRoleName`: add a
property there, its label under `page.settings.general` (or a card of its own in the General tab),
rather than a new setting and a new tab. A setting one slice owns outright — a group of options
about that slice's own behaviour — gets its own class and provider in that slice, as below
(`SeoSettings` in `Features/Seo/Core` is the model).

## 1. The setting class

A plain class whose public read-write properties are its values — any type System.Text.Json can
serialize — and whose property initializers are its **code default**:

```csharp
namespace Backend.Features.Invoices.Core;

/// <summary>
/// How invoices are numbered and when they fall due, registered as the <c>Invoicing</c> setting by
/// <see cref="InvoicesSettingsProvider"/>.
/// </summary>
public class InvoicingSettings
{
    public string NumberPrefix { get; set; } = "INV-";
    public int PaymentTermDays { get; set; } = 30;
    public bool IsReminderEnabled { get; set; } = true;
}
```

Beside it, the FluentValidation validator every resolved value is held to:

```csharp
sealed class InvoicingSettingsValidator : AbstractValidator<InvoicingSettings>
{
    public InvoicingSettingsValidator()
    {
        RuleFor(x => x.NumberPrefix).NotEmpty().MaximumLength(10);
        RuleFor(x => x.PaymentTermDays).InclusiveBetween(1, 365).WithErrorCode(ErrorCodes.InvoicingPaymentTermInvalid.Value);
    }
}
```

- The validator runs against the **merged** value a write would produce, never against the submitted
  properties alone, and against the default at startup — a default its own validator rejects stops the
  host. A class with nothing to check still gets an empty validator, so a rule added with a new
  property is enforced from the start.
- A rule declared `.WithErrorCode(ErrorCodes.X.Value)` is served localized from `error.server.<code>`
  (see `api-error-handling`); a plain rule keeps FluentValidation's own message.
- Properties are stored and served **camelCase** (`paymentTermDays`), matched case-insensitively on the
  way in; enums travel as camelCase strings. A `[JsonIgnore]` or `[JsonPropertyName]` is honoured.
- Merging is per **top-level** property. A property whose value is an object or a list is overridden
  whole, never member by member — split it into scalar properties if part of it should be overridable.

## 2. The provider

Each slice declares its settings in `Core/<X>SettingsProvider.cs`, beside the `<X>PermissionsProvider`
it may already have. Providers are discovered by reflection across the assembly (`SettingsFeature`),
so there is nothing to register:

```csharp
using Backend.Features.Settings.Core;

namespace Backend.Features.Invoices.Core;

/// <summary>Declares the settings the Invoices feature owns.</summary>
public class InvoicesSettingsProvider : ISettingDefinitionProvider
{
    /// <summary>The name <see cref="InvoicingSettings"/> is registered, stored and addressed under.</summary>
    public const string InvoicingSettingName = "Invoicing";

    public void Define(SettingDefinitionContext context)
    {
        context.Add<InvoicingSettings>(InvoicingSettingName, new InvoicingSettingsValidator());
    }
}
```

The name is global, stable and at most `SettingDefinitionContext.NameMaxLength` characters; it is the
stored key and the route segment (`/settings/{name}`), so renaming it orphans the stored override.
Composition refuses — at startup, not on the first read — a name declared twice (case-insensitively),
a class registered under two names, and a default that fails its validator.

## 3. Default from code or from configuration

With no option the default is the code default, `new T()`. Chain `.FromConfiguration("Section")` to
let a configuration section stand in for it when the deployment supplies it:

```csharp
context.Add<InvoicingSettings>(InvoicingSettingName, new InvoicingSettingsValidator())
    .FromConfiguration("Invoicing");
```

- The section is bound onto a fresh `new T()`, so a key it leaves out keeps its initializer. A missing
  section leaves the code default in place; a value that cannot be bound stops startup.
- It is read **once**, when the catalogue is composed at startup. Changing the section needs a restart,
  and the configured default is validated then exactly as the code default would be.
- Use it when a deployment needs a baseline without anyone signing in to set it — mail servers,
  external endpoints. `Backend.External.Email.EmailSettings` (`EmailSettingsProvider`) is the model:
  the `EmailSettings` section is its default and an administrator may override it.

## 4. Two layers, merged property by property

A value resolves **per property**, first layer with an answer winning:

1. the stored override;
2. the default — the configured one when its section is supplied, else the code default.

An override of only `PaymentTermDays` leaves `NumberPrefix` on its default, and a later change to the
default (a new initializer, a new configuration value) reaches it. A stored property the class no
longer declares, or one whose stored value no longer deserializes to its type, contributes nothing
and the property follows the default — so removing or retyping a property needs no data migration.

Overrides are `SettingValue` rows, at most one per setting, holding a JSON object of only the
properties set. Read them through the service, never the table.

## 5. Secrets: `[SecretSetting]`

Mark a password or an API key with `[SecretSetting]`. It must be a `string` (startup refuses any other
type). It is stored encrypted with ASP.NET Data Protection, never returned by the API — `GET` and `PUT`
answer it with no value, only `isSet` — and handed decrypted to the code that reads the setting.

Name the properties that decide where the secret is sent, with `nameof`:

```csharp
public string ApiUrl { get; set; } = "https://api.example.com";
public string ApiUser { get; set; } = string.Empty;

[SecretSetting(nameof(ApiUrl), nameof(ApiUser))]
public string ApiKey { get; set; } = string.Empty;
```

A bound secret is **never inherited apart from them**: when the override changes `ApiUrl` or
`ApiUser` without supplying its own `ApiKey`, the key resolves to `""` — repointing the integration
at another server does not hand that server the configured key (`EmailSettings.SmtpPassword` is
bound to the server, port and user name). Bind every secret to its destination; an unbound one
falls back to its default like any other property.

- A deployment running more than one instance must share the Data Protection key ring, as it already
  must for the auth cookie (`DataProtection:KeysPath`). A stored secret that no longer decrypts is
  logged and ignored, and the property follows the default.
- A validation failure on a secret is answered without its attempted value, but its message is sent
  as written — never put `{PropertyValue}` in the message of a rule on a secret.
- A setting that names **where the server connects** — a host, a URL, a port — turns
  `Settings.Update` into the power to make the server send requests (and secrets) anywhere. Validate
  it as tightly as its use allows: an absolute `http(s)` URL where a URL is meant
  (`SeoValidationRules.IsAbsoluteHttpUrl`), and loopback, private, link-local and cloud-metadata
  addresses refused where the server itself connects.

## 6. Reading a setting

Depend on `ISettingProvider`; every read returns a new instance, usable anywhere — a request,
anonymous or not, or a Hangfire job:

```csharp
var invoicing = await settingProvider.GetAsync<InvoicingSettings>(cancellationToken);
```

- A resolved setting holds its secrets **in plaintext**. Never return it from an endpoint, log it, put
  it in an exception message, or pass it (or a secret) as a job argument — Hangfire stores arguments
  unencrypted and shows them on its dashboard. A job resolves the setting itself when it runs, so a
  retry uses the value standing then. `/settings` is the only API surface for settings, and it masks
  secrets; an anonymous endpoint that serves part of one (the `/seo` endpoints serve `SeoSettings`)
  copies out only the public properties.
- Values are read from the database on the first read of a setting in a request or job and reused for
  the rest of it; the next request sees whatever was saved in between.
- A value decided when a session is created (sign-in, refresh — as `GeneralSettings` is) is read at
  that moment; a later change reaches the session only when it is next replaced, because settings
  revoke nothing.

## 7. Reacting to a change: `ISettingChangedHandler`

A slice that caches anything derived from a setting implements `ISettingChangedHandler` and
registers it as a singleton in its `<X>Feature.cs`
(`services.AddSingleton<ISettingChangedHandler>(...)`). After an override is replaced or removed,
`OnSettingChanged(settingName)` is called on every handler in the process that made the change; it
compares the name and drops its cache. It must neither throw nor do slow work. `SeoCache` is the
model: it invalidates when the `Seo` setting changes. Other API instances are not told — their
caches expire on their own clock — and the web server's cached copy of a public page is cleared by
the admin screen calling `revalidatePublicCache([...])` after the save (see the `seo` skill).

## 8. The `/settings` endpoints

Every declared setting is exposed with no endpoint of its own, under `Settings.View` /
`Settings.Update`:

| Endpoint | Does |
|---|---|
| `GET /settings` | Every setting as it resolves: per property its `value`, its `source` (`override` or `default`), `isSecret`, and for a secret `isSet` instead of a value |
| `PUT /settings/{name}` | Replaces the stored overrides with `{ "values": { … } }` — a property left out follows the default again. A secret follows its own rule: left out or sent `null`, its stored value is kept (so `{}` removes every override but a stored secret); sent `""`, its override is removed and it follows the default, subject to its bound properties |
| `DELETE /settings/{name}` | Removes the setting's overrides, stored secrets included. Idempotent |

A write is refused with `settingNotFound` (404), `settingPropertyUnknown`, `settingPropertyInvalid`
(the value does not deserialize to the property's type), or `settingValueInvalid` followed by the
validator's failures, each named `values.<camelCaseProperty>`.

## 9. The admin screen

`/admin/settings` (`app/[lang]/admin/settings`) edits the overrides through the RTK Query slice in
`store/api/settings`. It renders **one tab per setting it knows**, in a fixed order (`SETTINGS_TABS`
in `settings-manager.tsx` — `general`, `email`, `seo` — kept in the URL as `?tab=`, General first and
the default), and ignores a setting the API declares that it does not — so a new setting is reachable
over the API at once but appears on the screen only when it is given a card. A tab whose setting the
API does not declare is not shown, and a `?tab=` naming one falls back to the first tab. Every panel
stays mounted (the inactive ones `hidden`), so a half-edited form keeps its edits across a tab change:

1. the name in `SettingName` and a typed view model in `settings-dtos.ts`, and a `to<Name>Settings`
   mapper in `settings-mappers.ts` that returns `null` when a property is missing;
2. the form values, sources and submit shape in `_components/settings-form.ts`, with tests beside it;
3. a `<name>-settings-card.tsx` built from `settings-card-parts.tsx`, rendered by `settings-manager.tsx`
   — its tab added to `SETTINGS_TABS`, `TAB_ICONS`, `TAB_DEFAULT_LABELS` and the `panels` map;
4. labels and help text under `page.settings.<name>`, and the tab label as `page.settings.<name>.title`,
   in every resource file (see `localization`);
5. when the setting shapes a public page, the card calls `revalidatePublicCache([...])` after a
   successful save (`seo-settings-card.tsx`).

Each card shows where every value comes from (set here, or the default), offers a reset that removes
the overrides, and never pre-fills a secret. See `frontend-page` and `ui-component` for the screen
conventions.

## 10. Tests

A stored row changes what every request resolves to. So every class that writes one joins the
`Settings` collection by deriving from `SettingsTestsBase` (the SEO suite does, through
`SeoTestsBase`), whose teardown removes every stored setting row after each test. Inside it:

- write rows directly with `SetStoredValuesAsync(name, values)`, or through the endpoints with
  `AdminClientAsync()` / `SettingsClientAsync(params permissions)`; read what was stored with
  `StoredValuesAsync(name)`;
- never store a row that changes sign-in or any behaviour the rest of the suite relies on while it
  stands — a `General` row requiring email verification would refuse every unverified test account.
  Overlay it on one client instead: `OverlaySetting(client, settingName, values)` on `AppTestsBase`
  makes that client's requests (and only those) see `values` as the stored overrides
  (`SettingOverlays` in `Tests/Fakes`); `AllowSignup(client)` and `SignupVisitorClient()` use it to
  turn `General.IsSignupEnabled` on;
- to exercise the mechanism itself, use the test-only probe setting (`ProbeSettings`, `Tests/Fakes`),
  which nothing in the application reads;
- a setting behind a cache needs the cache dropped after the teardown removed the rows behind its
  back (`SeoTestsBase` invalidates `ISeoCache`).

A test that only reads a setting, or overlays it per client, needs no collection.

## Checklist

- [ ] Setting class in the owning slice's `Core`, property initializers as its code default
- [ ] Validator beside it; coded rules use `.WithErrorCode(ErrorCodes.X.Value)` with a shipped `error.server.<code>`
- [ ] `Core/<X>SettingsProvider.cs` registers it under a stable name, `.FromConfiguration(...)` if a deployment sets the baseline
- [ ] Every secret is a `string` marked `[SecretSetting]`, bound to the properties that decide where it goes
- [ ] Read through `ISettingProvider`; never returned, logged or passed to a job
- [ ] An `ISettingChangedHandler` if anything caches what the setting decides
- [ ] A card on `/admin/settings` if an administrator should edit it there, with translations in every locale
- [ ] Tests that store a row derive from `SettingsTestsBase` (the `Settings` collection); behaviour others rely on is overlaid per client
