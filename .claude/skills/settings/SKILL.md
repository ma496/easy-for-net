---
name: settings
description: Add or change a setting — a typed value the platform and each tenant may override (the setting class, its validator, the slice's SettingsProvider, a default from code or from configuration with FromConfiguration, [SecretSetting] properties), read it with ISettingProvider inside a request or with GetAsync<T>(tenantId) outside one, the /settings endpoints and the admin screen, and the Settings test collection. Use when behaviour should be configurable per tenant at run time rather than fixed in code or appsettings.
---

# Adding a setting

A **setting** answers *how does this behave here* — a value an administrator may change at run time,
for the whole platform or for one tenant. It is neither a permission (*may this caller do it*) nor a
feature (*does this tenant's plan include it*): changing a setting grants and withholds nothing, and
revokes no session. Reach for one when a value that would otherwise sit in `appsettings.json` should
differ per tenant, or be changed without a deployment.

The system lives in the `Settings` slice (`Features/Settings`). What other slices use is published
with `[AllowOutside]`: `ISettingDefinitionProvider`, `SettingDefinitionContext`,
`SettingDefinitionBuilder<T>`, `SecretSettingAttribute` and `ISettingProvider`, all in
`Backend.Features.Settings.Core`. Everything that resolves or stores a value stays private to the slice.

## 1. The setting class

A plain class whose public read-write properties are its values — any type System.Text.Json can
serialize — and whose property initializers are its **code default**:

```csharp
namespace Backend.Features.Invoices.Core;

/// <summary>
/// How invoices are numbered and when they fall due, registered as the <c>Invoicing</c> setting by
/// <see cref="InvoicesSettingsProvider"/> and resolved per tenant.
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
  whole, never member by member — split it into scalar properties if a tenant should override part of it.

## 2. The provider

Each slice declares its settings in `Core/<X>SettingsProvider.cs`, beside the `<X>PermissionsProvider`
and `<X>FeaturesProvider` it may already have. Providers are discovered by reflection across the
assembly (`SettingsFeature`), so there is nothing to register:

```csharp
namespace Backend.Features.Invoices.Core;

using Backend.Features.Settings.Core;

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
stored key and the route segment (`/settings/{name}`), so renaming it orphans every stored override.
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
  the `EmailSettings` section is its default and each scope may override it.

## 4. Three layers, merged property by property

A value resolves **per property**, first layer with an answer winning:

1. the acting tenant's own override;
2. the platform's override (a row with `TenantId == null`), inherited by every tenant;
3. the default — the configured one when its section is supplied, else the code default.

A tenant that overrides only `PaymentTermDays` still inherits `NumberPrefix` from the platform, and
the platform's later change to `NumberPrefix` reaches it. Resolving for no tenant is the platform's
answer: the default with the platform's overrides laid over it. A stored property the class no longer
declares, or one whose stored value no longer deserializes to its type, contributes nothing and the
property follows the layer below — so removing or retyping a property needs no data migration.

Overrides are `SettingValue` rows (`IMayHaveTenant`), at most one per scope and setting, holding a JSON
object of only the properties that scope set. Read them through the service, never the table.

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

A bound secret is **never inherited apart from them**: when a layer overrides `ApiUrl` or `ApiUser`
without supplying its own `ApiKey`, the key resolves to `""` — a tenant pointing the integration at its
own server does not hand that server the platform's key. Bind every secret to its destination; an
unbound one is inherited like any other property.

- A deployment running more than one instance must share the Data Protection key ring, as it already
  must for the auth cookie. A stored secret that no longer decrypts is logged and ignored, and the
  property follows the layer below.
- A validation failure on a secret is answered without its attempted value, but its message is sent
  as written — never put `{PropertyValue}` in the message of a rule on a secret.
- A setting that names **where the server connects** — a host, a URL, a port — is chosen by every
  tenant administrator holding `Settings.Update`, so it is a server-side request forgery surface. Its
  validator must refuse loopback, private, link-local and cloud-metadata addresses (or check an
  allow-list), and the code that connects must check the resolved address again before connecting.

## 6. Reading a setting

Depend on `ISettingProvider`; every read returns a new instance.

```csharp
// Inside a request or a job that has opened a scope: the acting tenant, or the platform in platform scope.
var invoicing = await settingProvider.GetAsync<InvoicingSettings>(cancellationToken);

// Anywhere, naming the target: that tenant's value, or the platform's when tenantId is null.
var invoicing = await settingProvider.GetAsync<InvoicingSettings>(tenantId, cancellationToken);
```

- The scope-less overload refuses, as `ITenantContext.CurrentTenantId` does, when no scope has been
  established. **Outside a request** — a Hangfire job, a hosted service — capture the tenant id when
  the work is enqueued, pass it as a job argument and call `GetAsync<T>(tenantId)`; resolve at run time
  rather than passing the settings object, so a retry uses the value standing when it runs.
- A resolved setting holds its secrets **in plaintext**. Never return it from an endpoint, log it, put
  it in an exception message, or pass it (or a secret) as a job argument — Hangfire stores arguments
  unencrypted and shows them on its dashboard. `/settings` is the only API surface for settings, and it
  masks secrets.
- An **anonymous endpoint** still carries a signed-in caller's tenant scope, so never read the ambient
  scope there — name the target: the platform (`GetAsync<T>(null)`) for account-level work (sign-up,
  password reset, verification mail) — read from the ambient scope, a tenant administrator would
  decide it for another account — or the tenant the request is entering once its membership has been
  checked, as sign-in does (and refresh and tenant switch, which are not anonymous, do too).
- Values are read from the database on the first read of a setting in a request or job and reused for
  the rest of it; the next request sees whatever was saved in between.
- A value decided when a session is created (sign-in, refresh) is read at that moment; a later change
  reaches the session only when it is next replaced, because settings revoke nothing.

## 7. The `/settings` endpoints

Every declared setting is exposed with no endpoint of its own. They act on the **acting scope** — the
tenant's own row inside a tenant, the platform's in platform scope — under `Settings.View` /
`Settings.Update`, both `PermissionScope.Both` and gated on no feature:

| Endpoint | Does |
|---|---|
| `GET /settings` | Every setting as the acting scope resolves it: per property its `value`, its `source` (`tenant`, `platform` or `default`), `isSecret`, and for a secret `isSet` instead of a value |
| `PUT /settings/{name}` | Replaces the acting scope's overrides with `{ "values": { … } }` — a property left out follows the layer below again. A secret follows its own rule: left out or sent `null`, this scope's stored value is kept (so `{}` removes every override but a stored secret); sent `""`, this scope's override of it is removed and it follows the layer below, subject to its bound properties |
| `DELETE /settings/{name}` | Removes the acting scope's overrides of the setting, stored secrets included. Idempotent |

A write is refused with `settingNotFound` (404), `settingPropertyUnknown`, `settingPropertyInvalid`
(the value does not deserialize to the property's type), or `settingValueInvalid` followed by the
validator's failures, each named `values.<camelCaseProperty>`.

## 8. The admin screen

`/admin/settings` (`app/[lang]/admin/settings`) edits the acting scope's overrides through the RTK Query
slice in `store/api/settings`. It renders **one card per setting it knows**, in a fixed order, and
ignores a setting the API declares that it does not — so a new setting is reachable over the API at
once but appears on the screen only when it is given a card:

1. the name in `SettingName` and a typed view model in `settings-dtos.ts`, and a `to<Name>Settings`
   mapper in `settings-mappers.ts` that returns `null` when a property is missing;
2. the form values, sources and submit shape in `_components/settings-form.ts`, with tests beside it;
3. a `<name>-settings-card.tsx` built from `settings-card-parts.tsx`, rendered by `settings-manager.tsx`;
4. labels and help text under `page.settings.<name>` in every resource file (see `localization`).

Each card shows where every value comes from (set here, inherited from the platform, or the default),
offers a reset that removes this scope's overrides, and never pre-fills a secret. See `frontend-page`
and `ui-component` for the screen conventions.

## 9. Tests

A tenant's overrides are isolated by giving each test a fresh tenant, but a **platform** row changes
what every tenant resolves to. So every class that writes a platform row joins the `Settings`
collection by deriving from `SettingsTestsBase`, whose teardown removes every platform row after each
test. Inside it:

- write layers directly with `SetPlatformValuesAsync` / `SetTenantValuesAsync`, or through the
  endpoints with `TenantClientAsync(tenantId)` and `PlatformClientAsync()`; read what was stored with
  `StoredValuesAsync(name, tenantId)`;
- prove resolution on a tenant the test created, never on a seeded one;
- never write a platform override that changes sign-in or any behaviour the rest of the suite relies
  on while it stands — simulate it for a tenant the test owns instead, with
  `Service<PlatformSettingOverlays>().Apply(tenantId, name, values)` (`Tests/Fakes`), which lays the
  values over the platform layer for that tenant alone;
- to exercise the mechanism itself, use the test-only probe setting (`Tests/Fakes`), which nothing in
  the application reads.

A test that only reads a setting, or writes a tenant row for its own tenant, needs no collection.

## Checklist

- [ ] Setting class in the owning slice's `Core`, property initializers as its code default
- [ ] Validator beside it; coded rules use `.WithErrorCode(ErrorCodes.X.Value)` with a shipped `error.server.<code>`
- [ ] `Core/<X>SettingsProvider.cs` registers it under a stable name, `.FromConfiguration(...)` if a deployment sets the baseline
- [ ] Every secret is a `string` marked `[SecretSetting]`, bound to the properties that decide where it goes
- [ ] Read through `ISettingProvider`; `GetAsync<T>(tenantId)` outside a request, `GetAsync<T>(null)` for account-level decisions
- [ ] A card on `/admin/settings` if an administrator should edit it there, with translations in every locale
- [ ] Tests that write a platform row derive from `SettingsTestsBase` (the `Settings` collection)
