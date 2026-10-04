---
name: frontend-tests
description: Write and run Vitest tests for the web app in src/frontend/web — colocated *.test.ts files, what is worth testing (pure helpers, route and tenant-scope rules, reducers, middleware, the tenant-change page load, client/API mirrors, locale files) and how to structure cases. Use when changing frontend logic that has rules worth pinning down.
---

# Frontend tests

Vitest, run with `npm run test` (`vitest run`) from `src/frontend/web`. A single file:

```sh
npx vitest run lib/utils/tenant-routing.test.ts
npx vitest            # watch mode while iterating
```

`vitest.config.mts` maps the `@/` alias from `tsconfig.json` and collects **`**/*.test.ts` only** — a
`.test.tsx` file is never run. There is no DOM environment and no setup file: the suite covers logic,
not rendered components. Keep it that way unless a task genuinely requires a browser, where adding
`jsdom`, an environment and a `.tsx` include is a deliberate decision, not a side effect.

Vitest does not read `tsconfig.json` paths, and TypeScript erases an import used only as a type. So an
import of a *value* through `@/` — `import { setUserInfo } from '@/store/slices'` — is resolved by the
config, while a type-only one needs nothing; a module whose own imports go through the alias loads in
a test only because of it.

## Layout

Tests are **colocated** with the code they cover and named `<file>.test.ts`:

```
lib/utils/redirect.test.ts                 lib/utils/tenant-routing.test.ts
lib/utils/upload-limit.test.ts             lib/utils/api-error-helpers.test.ts
lib/notifications/hub-reconnect.test.ts    components/ui/data-table/card-layout.test.ts
store/slices/authSlice.test.ts             store/middlewares/rtk-error-middleware.test.ts
store/tenant-cache.test.ts                 store/api/settings/settings/settings-mappers.test.ts
styles/tokens.test.ts                      i18n/locales.test.ts
i18n/tenant-screens.test.ts                i18n/resolve-locale.test.ts
i18n/locale-guard.test.ts                  i18n/translate.test.ts
i18n/locale-routing-loop.test.ts
allow.test.ts  auth-urls.test.ts  feature-names.test.ts
app/[lang]/(auth)/select-tenant/_components/select-tenant-view.test.ts
```

Import the unit under test by relative path (`from './redirect'`), everything else through the `@/`
alias.

## Shape

```ts
import { describe, expect, it } from 'vitest'
import { isValidRedirectPath } from './redirect'

describe('isValidRedirectPath', () => {
  it('accepts local application paths', () => {
    expect(isValidRedirectPath('/admin/users')).toBe(true)
  })

  it.each(['https://example.com', '//example.com', '/signin'])('rejects unsafe redirect %s', (path) => {
    expect(isValidRedirectPath(path)).toBe(false)
  })
})
```

- One `describe` per exported function (or unit), named after it.
- `it('<behaviour in plain words>')` — describe the rule, not the mechanics.
- `it.each([...])` for a family of inputs that share one expectation.
- Build fixtures with a small local factory taking overrides, rather than repeating object literals.
  The session shape carries tenant fields — leave none out:

```ts
const tenant = (id: string): GetUserInfoTenant => ({ id, name: `Tenant ${id}`, identifier: `t-${id}` })

const userInfo = (overrides: Partial<GetUserInfoResponse> = {}): GetUserInfoResponse => ({
  id: 'user-1', username: 'someone', email: 'someone@example.com',
  isPlatform: false, tenants: [], roles: [],
  ...overrides,
})

const stateWithPermissions = (...permissions: string[]): AuthState => ({
  isAuthenticated: true, activeTenant: undefined, tenants: [],
  user: userInfo({ roles: [{ id: 'role-id', name: 'Admin', permissions: permissions.map((name) => ({ id: name, name, displayName: name })) }] }),
})
```

## What to cover

Worth a test — anything with a rule that would be expensive to get wrong and is testable without a
browser:

- **Security- and routing-shaped helpers**: redirect validation, `isAllowed`, `getMatchedAuthUrl`
  (including the "two entries match the same path" error), the tenant-scope rules in
  `tenant-routing.ts` (`isPathAvailable`, `resolveTenantLanding`, …) for a platform account in no
  tenant, one inside a tenant, and an ordinary member. A new `/admin` route that is not tenant-only
  gets its case there.
- **Data shaping**: `getApiErrorMessages`, upload-limit arithmetic, export row mapping, `shortName`.
- **Reducers** — call `slice.reducer(state, action)` and assert on the state it returns.
- **Middleware** — run an action through `middleware(api)(next)(action)` with a recording `dispatch`
  and assert on what it dispatched, and on what it left alone.
- **Browser globals** a helper touches: `tenant-cache.test.ts` stubs `window` with `vi.stubGlobal`
  (a recording `location.replace`, an in-memory `sessionStorage`), asserts the full page load and the
  notice it hands to the next document, and `vi.unstubAllGlobals()` in `afterEach`.
- **Client/API mirrors**: `allow.test.ts` and `feature-names.test.ts` restate the backend constants and
  fail when `allow.ts` / `feature-names.ts` drift — add the new constant there when you add a
  permission or a feature.
- **Locales**: `i18n/locales.test.ts` holds `i18nConfig.locales` equal to the backend's resource files;
  `i18n/tenant-screens.test.ts` reads those files — extend its key lists when a screen's strings must
  never go missing (see `localization`). Key parity across locales is the backend's test, not this one.
- **Wiring that cannot be rendered here** (a guard calling a helper, a screen using logical
  utilities) may be checked by reading the source with `readFileSync`. Such a test must first assert
  that it found the files it scans, so a moved file cannot make it pass by being absent.

Not worth it here — component rendering, styling, and the request/response shape of RTK Query
endpoints. Endpoint behaviour is covered on the API side instead; see the `backend-tests` skill.

## Rules

- Tests must be deterministic: no network, no real timers, no reliance on `localStorage` unless the
  test sets it up; helpers that guard with `typeof window === 'undefined'` are testable as-is.
- Assert on behaviour, not implementation details — call the exported function, check the result.
- When you fix a bug in a helper, add the failing input as a case first.
- `npm run test`, `npm run lint` and `npx tsc --noEmit` all need to pass before the change is done —
  Vitest and ESLint both pass on code that fails the type check.
