---
name: api-error-handling
description: Report an API failure end-to-end — ThrowError with an ErrorCodes constant, plan refusals (FeatureDisabledException / FeatureLimitExceededException), the ProblemDetails shapes the API emits, and the web side (getApiErrorMessages, ApiErrorMessages, apiErrorAlert, error.server.* keys). Use when adding a new failure case or when an error surfaces untranslated in the UI.
---

# Errors, end to end

A new failure case is a three-part change: a code on the API, a translation key on the web, and the
right component to show it.

## 1. API — raise it

```csharp
if (usernameExists)
{
    ThrowError("Username already exists", ErrorCodes.UsernameAlreadyExists);
}
```

Overloads on `EndpointExtension` (`Backend.Extensions`):

| Call | Produces |
| --- | --- |
| `ThrowError(message, code)` | request-level error (empty property name) |
| `ThrowError(x => x.Email, message, code)` | error attached to that request property |
| `ThrowError("PropertyName", message, code)` | same, with the name spelled out |

All three add a `ValidationFailure` and throw `ValidationFailureException`, so the response is a
400 ProblemDetails with the code included (`IndicateErrorCode = true` in `Program.cs`).

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

New codes go in `src/backend/Source/ErrorHandling/ErrorCodes.cs` as camelCase string constants
(`usernameAlreadyExists`, `systemCreatedRoleCannotBeDeleted`). Reuse an existing code when it already
describes the situation — the list is deliberately shared across features.

## 2. Response shape

```json
{
  "status": 400,
  "title": "Validation Error",
  "errors": [{ "name": "username", "code": "usernameAlreadyExists", "reason": "Username already exists" }]
}
```

`name` is the camelCased request property (empty for request-level errors; the feature name for plan
refusals), `code` is the `ErrorCodes` constant or a FluentValidation code, `reason` is the raw
message. Titles are transformed by status: 400 → "Validation Error", 404 → "Not Found"; the
hand-shaped responses above carry their own ("Feature Disabled", "Db Update Failed", …).

## 3. Web — translate it

`getApiErrorMessages(error, t, ignoreStatuses?)` (in `lib/utils/api-error-helpers.ts`) turns any RTK
error shape into `{ title, messages }`:

- **400 with `errors`** — each error is looked up as **`error.server.{code}`**, passing the
  translated field name as `${propertyName}`, falling back to the raw `reason` when the key is
  missing. The field name is mapped by stripping a `Normalized` suffix and lowercasing the first
  letter (`EmailNormalized` → `email`).
- **401 / 403 / 404 / 413 / 415 / 500** — the first error's code, translated via
  `error.server.{code}` when that key exists, else the generic `error.{status}.message`. This is how
  `featureDisabled` or `tenantSuspended` reaches the user instead of "Forbidden".
- Title is `error.{status}.title` (`common.error` for anything else).

So every new code needs a key in the API's `Features/Localization/Core/Resources/en.json` (and every
other shipped resource file there):

```json
"error": {
  "400": { "title": "Bad Request" },
  "server": {
    "usernameAlreadyExists": "Username already exists",
    "featureLimitExceeded": "Your plan's limit for this has been reached."
  }
}
```

A message that interpolates `${propertyName}` also needs a top-level key for that field name,
because the name is itself translated (`t(fieldName)`).

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
account info; if the refresh fails it signs the user out and redirects to `/signin?redirect=…`. Other
404s reach the screen.

## Checklist

- [ ] Constant in `ErrorHandling/ErrorCodes.cs` (or an existing one reused)
- [ ] `ThrowError` with that constant, the right `Send.*` helper, or the feature exception for a plan refusal
- [ ] `error.server.<code>` key in every backend resource file
- [ ] The screen renders `ApiErrorMessages` or calls `apiErrorAlert`
- [ ] An endpoint test asserts the status and, where it matters, the error name/code
