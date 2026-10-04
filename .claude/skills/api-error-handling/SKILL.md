---
name: api-error-handling
description: Report an API failure end-to-end — ThrowError with an ErrorCodes constant, plan refusals (FeatureDisabledException / FeatureLimitExceededException), the ProblemDetails shapes the API emits, how the API localizes a coded error before it ever reaches the browser, and the web side (getApiErrorMessages, getErrorCode, ApiErrorMessages, apiErrorAlert). Use when adding a new failure case or when an error surfaces wrong or untranslated in the UI.
---

# Errors, end to end

A new failure case is a two-part change: a code on the API, with a translation key beside it in every
resource file, and the right component on the web to show the message the API sends. The web holds no
translation of its own for a coded error — it shows the `reason` the API already localized.

## 1. API — raise it

```csharp
if (usernameExists)
{
    this.ThrowError(ErrorCodes.UsernameAlreadyExists);
}
```

A call site names only the code — never an English message. The code is the resource key
(`error.server.<code>`); the message the caller would otherwise have typed would drift from what
that key ships and would have to be kept in step across every shipped resource file by hand.

Overloads on `EndpointExtension` (`Backend.Extensions`), each taking an `ErrorCode` from
`ErrorHandling/ErrorCodes.cs` and no message:

| Call | Produces |
| --- | --- |
| `ThrowError(code)` | request-level error (filed under `generalErrors`) |
| `ThrowError(code, statusCode)` | request-level error answered with that status instead of 400 (`settingNotFound` → 404) |
| `ThrowError(x => x.Email, code)` | error attached to that request property |
| `ThrowError("PropertyName", code)` | same, with the name spelled out |

Called with the `this.` receiver: FastEndpoints' own `Endpoint<TRequest,TResponse>` declares
instance overloads named `ThrowError`, none of which accepts an `ErrorCode`, so through `this.` the
compiler binds these extensions. A bare `ThrowError(ErrorCodes.X)` never considers extension methods
and fails to compile, so a missing receiver is a build error, not a silent misbind. Each adds a `ValidationFailure`
— its message set from `ErrorLocalization.ResolveEnglishFallback`, the shipped English text for the
code with no database involved — and throws, so the response is a ProblemDetails (400 unless a status
is given) with the code included (`IndicateErrorCode = true` in `Program.cs`).

`ErrorCode` (`ErrorHandling/ErrorCode.cs`) is a `readonly record struct` wrapping the code string,
deliberately with no implicit conversion to `string`: an applicable instance method always wins over
an extension, so an implicit conversion would let `this.ThrowError(ErrorCodes.X)` bind to
FastEndpoints' own `ThrowError(string message, int? statusCode)`, dropping the code and sending the
raw code string as a throwaway message instead. Read `.Value` explicitly wherever a plain string is
actually required — a `switch` case, an attribute argument, a dictionary key, FluentValidation's own
`WithErrorCode(string)`.

Use the `Send.*` helpers where the situation is not a validation failure: `Send.NotFoundAsync` for a
missing row, `Send.UnauthorizedAsync` when there is no current user, `Send.OkAsync` for a deliberate
no-op. Never throw a bare exception for an expected failure — `ExceptionProcessor` turns unhandled
ones into 500 `internalServerError`.

Refusals that are answered for you — do not re-implement them:

| Source | Status / code |
| --- | --- |
| Not signed in / missing permission (`AuthorizationRefusalResultHandler`) | 401 `authenticationRequired` / 403 `permissionDenied` |
| `featureChecker.CheckEnabledAsync(...)` → `FeatureDisabledException` | 403 `featureDisabled` (error `name` = the feature) |
| `throw new FeatureLimitExceededException(featureName, limit)` | 403 `featureLimitExceeded` |
| `DbUpdateException` from PostgreSQL | 400 `duplicateValue` / `duplicatePropertyValue` / `requiredFieldMissing` / `requiredPropertyFieldMissing` / `referencedRecordNotFound` / `invalidValueProvided` / `databaseError` |
| Body over `Payload:MaximumSize` (`ToLargePayloadProcessor`) | 413 `payloadTooLarge` |
| Wrong content type (`UnsupportedMediaTypeResponseProcessor`) | 415, no code |

Plan checks throw rather than `ThrowError`, because entitlement is a business precondition, not a
validation failure — see the `feature-management` skill. Tenancy refusals (`noActiveTenant`,
`notTenantMember`, `tenantSuspended`, `tenantRequired`, `crossTenantFileAccess`, …) are ordinary
`ThrowError` codes; see the `multi-tenancy` skill for when each applies.

New codes go in `src/backend/Source/ErrorHandling/ErrorCodes.cs` as `public static readonly ErrorCode`
members carrying a camelCase value (`usernameAlreadyExists`, `systemCreatedRoleCannotBeDeleted`).
Reuse an existing code when it already describes the situation — the list is deliberately shared
across features.

## 2. Response shape

```json
{
  "status": 400,
  "title": "Validation Error",
  "errors": [{ "name": "username", "code": "usernameAlreadyExists", "reason": "Username already exists" }]
}
```

`name` is the camelCased request property (`generalErrors` for request-level errors; the feature name
for plan refusals), `code` is the `ErrorCodes` constant's `.Value` or a FluentValidation code, `reason` is the
localized message. Titles are transformed by status: 400 → "Validation Error", 404 → "Not Found"; the
hand-shaped responses above carry their own ("Feature Disabled", "Db Update Failed", …).

## 3. Web — show it as sent

`store/api/_app-api.ts`'s `baseQuery` sets `Accept-Language` on every request to the locale segment
of the current URL (`localeFromPathname`, `@/i18n`), and `i18n/server.ts`'s own fetch of the resource
dictionary does the same — so the API always knows which culture to answer a refusal in.

The server localizes every coded error itself: `Backend.ErrorHandling` looks a `ValidationFailure`'s
`ErrorCode` up as **`error.server.{code}`** in that culture (with the tenant's and the platform's
overrides applied, same as any other key), interpolates `${propertyName}` with the field name
translated the same way, and puts the result in `reason` before the response ever reaches the
browser. This holds for every error shape `ExceptionProcessor` and `AuthorizationRefusalResultHandler`
produce, not just 400s: a `featureDisabled` 403 or an `authenticationRequired` 401 carries the same
localized `reason` a validation failure does — and this reaches even a refusal built before the
endpoint pipeline establishes the acting tenant (the 401/403 from `AuthorizationRefusalResultHandler`), which
still resolves with the session's own tenant overrides. A code with no `error.server.*` entry — a
bare FluentValidation rule — keeps FluentValidation's own built-in per-culture message instead: that
message is already translated into the request's culture by FluentValidation itself, not by this
application's resource dictionary, so the field name inside the sentence is the raw property name
rather than the translated label `${propertyName}` substitutes. Either way, a localization failure —
no resolvable culture, no reachable resource store — falls back to the shipped English text for the
code (read with no database involved, the same text a freshly thrown `ThrowError` starts its
own `ValidationFailure` with) rather than failing the response.

The web therefore does no translation of its own for any of it.
`getApiErrorMessages(error, t, ignoreStatuses?)` (in `lib/utils/api-error-helpers.ts`) turns any RTK
error shape into `{ title, messages }`:

- **A body with `errors`** (whatever the status) — one message per error, read straight from
  `reason` and shown unchanged.
- **A body with no `errors`** — `detail`, then `message`, then `title`, whichever the body carries;
  failing all three, the generic `error.{status}.message`.
- **Title** is always this application's own `error.{status}.title` (`common.error` for a status it
  has no title for) — the one thing here that is never itself localized by a response body.
- **Network/parsing failures** (`FETCH_ERROR`, `PARSING_ERROR`, `CUSTOM_ERROR`) fall back to the
  transport's own message under the `common.error` title, since there was no response to localize.

So every new code needs a key in the API's `Features/Localization/Core/Resources/en.json` (and every
other shipped resource file there) — **not** a key on the web, which has none of its own:

```json
"error": {
  "400": { "title": "Bad Request" },
  "server": {
    "usernameAlreadyExists": "Username already exists",
    "featureLimitExceeded": "Your plan's limit for this has been reached."
  }
}
```

`getErrorCode(error)` reads the first error's code back out, for the rare screen that has to branch
on *what* refused a request rather than just display it (a feature gate, a tenant refusal) — showing
the refusal is never this application's job, translating it least of all.

## 4. Show it

| Situation | Use |
| --- | --- |
| A query failed and the screen cannot render | `<ApiErrorMessages error={error} />` in place of the content |
| A form submit failed | `apiErrorAlert(result.error)` — a modal listing the messages |
| A mutation succeeded | `successToast.fire({ text: t('page.users.createSuccess') })` |
| A soft failure the API reports in the body | `await errorAlert({ text: response.data.message })` |

```tsx
const result = await createUser(payload)
if (result.error) { apiErrorAlert(result.error); return }
```

`ApiErrorMessages` accepts `className`, `dismissible` (default `true`) and `ignoreStatuses` (skip
codes the screen handles itself, e.g. a 404 that renders an empty state); `apiErrorAlert` takes the
same `ignoreStatuses` as its second argument. Alert/toast helpers all come from `@/lib/utils`:
`toast`, `successToast`, `errorToast`, `sweetAlert`, `successAlert`, `errorAlert`, `warningAlert`,
`infoAlert`, `confirmAlert`, `confirmDeleteAlert` — they already localize their default buttons.

Prefer preventing a plan refusal to reporting it: disable the action when the plan or seat count says
the API would refuse (see `feature-management`).

## Transport-level failures

`rtkErrorMiddleware` catches rejected queries with `status === 'FETCH_ERROR'` (API unreachable) and
dispatches `showServiceUnavailable()`, which swaps in `ServiceUnavailableView`. Do not duplicate
that handling per screen.

A 401 (and a 404 from `/account/get-info`) is intercepted earlier by `baseQueryWithReauth`, which
serializes a refresh attempt through an `async-mutex`, retries the original request and re-reads the
account info; if the refresh fails it signs the user out and, when the page requires a session,
redirects to `/signin?redirect=…`. Other
404s reach the screen.

## Checklist

- [ ] Constant in `ErrorHandling/ErrorCodes.cs` (or an existing one reused)
- [ ] `ThrowError` with that constant and no message, the right `Send.*` helper, or the feature exception for a plan refusal
- [ ] `error.server.<code>` key in every backend resource file
- [ ] The screen renders `ApiErrorMessages` or calls `apiErrorAlert`
- [ ] An endpoint test asserts the status and, where it matters, the error name/code
