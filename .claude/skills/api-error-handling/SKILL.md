---
name: api-error-handling
description: Report an API failure end-to-end — ThrowError with an ErrorCodes constant, the ProblemDetails shape FastEndpoints emits, and the web side (getApiErrorMessages, ApiErrorMessages, apiErrorAlert, error.server.* keys). Use when adding a new failure case or when an error surfaces untranslated in the UI.
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

Overloads on `EndpointExtension`:

| Call | Produces |
| --- | --- |
| `ThrowError(message, code)` | request-level error (empty property name) |
| `ThrowError(x => x.Email, message, code)` | error attached to that request property |
| `ThrowError("PropertyName", message, code)` | same, with the name spelled out |

All three add a `ValidationFailure` and throw `ValidationFailureException`, so the response is a
400 ProblemDetails with the code included (`IndicateErrorCode = true` in `Program.cs`).

Use the other `Send.*` helpers where the situation is not a validation failure:
`Send.NotFoundAsync` for a missing row, `Send.UnauthorizedAsync` when there is no current user,
`Send.OkAsync` for a deliberate no-op. Never throw a bare exception for an expected failure —
`ExceptionProcessor` turns unhandled ones into `internalServerError` with a 500.

New codes go in `src/backend/Source/ErrorHandling/ErrorCodes.cs` as camelCase string constants
(`usernameAlreadyExists`, `defaultRoleCannotBeDeleted`). Reuse an existing code when it already
describes the situation — the list is deliberately shared across features.

## 2. Response shape

```json
{
  "status": 400,
  "title": "Validation Error",
  "errors": [{ "name": "username", "code": "usernameAlreadyExists", "reason": "Username already exists" }]
}
```

`name` is the camelCased request property (empty for request-level errors), `code` is the
`ErrorCodes` constant or a FluentValidation code, `reason` is the raw message. Titles are
transformed by status: 400 → "Validation Error", 404 → "Not Found".

## 3. Web — translate it

`getApiErrorMessages` (in `lib/utils/api-error-helpers.ts`) turns any RTK error shape into
`{ title, messages }`. For each validation error it looks up **`error.server.{code}`** in the
dictionary, passing the field name as `${propertyName}`, and falls back to the raw `reason` when the
key is missing. It also strips a `Normalized` suffix and lowercases the first letter to map
`EmailNormalized` → `email`.

So every new code needs a key in `public/locales/en.json` (and the other locales when the project is
multi-language):

```json
"error": {
  "400": { "title": "Validation Error" },
  "server": {
    "usernameAlreadyExists": "This username is already taken.",
    "duplicateValue": "${propertyName} already exists."
  }
}
```

Because the property name is itself translated (`t(fieldName)`), a message that interpolates
`${propertyName}` needs a top-level key for that field name too.

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

`ApiErrorMessages` accepts `dismissible` and `ignoreStatuses` (skip codes the screen handles
itself, e.g. a 404 that renders an empty state). Alert/toast helpers all come from `@/lib/utils`:
`toast`, `successToast`, `errorToast`, `sweetAlert`, `successAlert`, `errorAlert`, `warningAlert`,
`infoAlert`, `confirmAlert`, `confirmDeleteAlert` — they already localize their default buttons.

## Transport-level failures

`rtkErrorMiddleware` catches rejected queries with `status === 'FETCH_ERROR'` (API unreachable) and
dispatches `showServiceUnavailable()`, which swaps in `ServiceUnavailableView`. Do not duplicate
that handling per screen.

401/404 are intercepted earlier by `baseQueryWithReauth`, which serializes a refresh attempt through
an `async-mutex`, retries the original request, and on failure signs the user out and redirects to
`/signin?redirect=…`.

## Checklist

- [ ] Constant in `ErrorHandling/ErrorCodes.cs` (or an existing one reused)
- [ ] `ThrowError` with that constant, or the right `Send.*` helper
- [ ] `error.server.<code>` key in every shipped locale file
- [ ] The screen renders `ApiErrorMessages` or calls `apiErrorAlert`
- [ ] An endpoint test asserts the status and, where it matters, the error name/code
