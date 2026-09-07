---
name: frontend-tests
description: Write and run Vitest tests for the web app in src/frontend/web — colocated *.test.ts files, what is worth testing (pure logic in lib/utils, hooks, guards) and how to structure cases. Use when changing frontend logic that has rules worth pinning down.
---

# Frontend tests

Vitest, run with `npm run test` (`vitest run`) from `src/frontend/web`. A single file:

```sh
npx vitest run lib/utils/redirect.test.ts
npx vitest            # watch mode while iterating
```

There is no `vitest.config.ts` — the defaults plus the TypeScript path aliases are enough, because
the suite covers **pure logic**, not rendered components. Keep it that way unless a task genuinely
requires a DOM environment; adding one means adding `jsdom` and a config file, which is a
deliberate decision, not a side effect.

## Layout

Tests are **colocated** with the code they cover and named `<file>.test.ts`:

```
lib/utils/redirect.ts
lib/utils/redirect.test.ts
lib/utils/authentication-and-authorization.ts
lib/utils/authentication-and-authorization.test.ts
```

Import the unit under test by relative path (`from './redirect'`), everything else through the
`@/` alias.

## Shape

```ts
import { describe, expect, it } from 'vitest'
import { isValidRedirectPath } from './redirect'

describe('isValidRedirectPath', () => {
  it('accepts local application paths', () => {
    expect(isValidRedirectPath('/admin/users/list')).toBe(true)
  })

  it.each(['https://example.com', '//example.com', '/signin'])('rejects unsafe redirect %s', (path) => {
    expect(isValidRedirectPath(path)).toBe(false)
  })
})
```

- One `describe` per exported function, named after it.
- `it('<behaviour in plain words>')` — describe the rule, not the mechanics.
- `it.each([...])` for a family of inputs that share one expectation.
- Build fixtures with a small local factory rather than repeating object literals:

```ts
const stateWithPermissions = (...permissions: string[]): AuthState => ({
  isAuthenticated: true,
  user: { id: 'user-id', username: 'user', email: 'user@example.com',
           roles: [{ id: 'role-id', name: 'Admin', permissions: permissions.map((name) => ({ id: name, name, displayName: name })) }] },
})
```

## What to cover

Worth a test — anything with a rule that would be expensive to get wrong and is testable without a
browser:

- security-shaped helpers: redirect validation, `isAllowed` permission logic, auth-cookie checks
- URL/route matching, such as `getMatchedAuthUrl` semantics (including the "two entries match the
  same path" error)
- data shaping: export row mapping, error-message extraction (`getApiErrorMessages`), name/label
  formatting (`shortName`), normalization helpers

Not worth it here — component rendering, RTK Query wiring, and styling. Endpoint behaviour is
covered on the API side instead; see the `backend-tests` skill.

## Rules

- Tests must be deterministic: no network, no real timers, no reliance on `localStorage` unless the
  test sets it up; helpers that guard with `typeof window === 'undefined'` are testable as-is.
- Assert on behaviour, not implementation details — call the exported function, check the result.
- When you fix a bug in a helper, add the failing input as a case first.
- `npm run test` and `npm run lint` both need to pass before the change is done.
