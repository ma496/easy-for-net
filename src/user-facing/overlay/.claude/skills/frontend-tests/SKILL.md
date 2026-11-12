---
name: frontend-tests
description: Write and run Vitest tests for the web app in src/frontend/web — colocated *.test.ts files, what is worth testing (pure helpers, the public/account/admin route rules and the guard redirect, SEO metadata/sitemap/robots mapping, the revalidation rule, reducers, middleware, source-reading rules, client/API mirrors, locale files) and how to structure cases. Use when changing frontend logic that has rules worth pinning down.
---

# Frontend tests

Vitest, run with `npm run test` (`vitest run`) from `src/frontend/web`. A single file:

```sh
npx vitest run lib/seo/metadata.test.ts
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
lib/utils/redirect.test.ts                 lib/paths.test.ts
lib/utils/upload-limit.test.ts             lib/utils/api-error-helpers.test.ts
lib/seo/metadata.test.ts                   lib/seo/sitemap-robots.test.ts
lib/revalidate-rule.test.ts                lib/server/api-fetch.test.ts
lib/notifications/hub-reconnect.test.ts    components/ui/data-table/card-layout.test.ts
components/layouts/guard-redirect.test.ts  store/slices/authSlice.test.ts
store/middlewares/rtk-error-middleware.test.ts
store/api/settings/settings/settings-mappers.test.ts
styles/tokens.test.ts                      i18n/locales.test.ts
i18n/default-texts.test.ts                 i18n/dictionary-fetch.test.ts
i18n/resolve-locale.test.ts                i18n/locale-guard.test.ts
i18n/translate.test.ts                     i18n/locale-routing-loop.test.ts
allow.test.ts  auth-urls.test.ts  proxy.test.ts
app/[lang]/admin/seo/_components/seo-page-form-values.test.ts
```

Import the unit under test by relative path (`from './redirect'`), everything else through the `@/`
alias.

## Shape

```ts
import { describe, expect, it } from 'vitest'
import { isValidRedirectPath } from './redirect'

describe('isValidRedirectPath', () => {
  it('accepts local application paths', () => {
    expect(isValidRedirectPath('/users')).toBe(true)
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
  The session shape is `AuthState` (`user`, `isAuthenticated`) over `GetUserInfoResponse` — the
  account and its roles, each carrying its permissions:

```ts
const userInfo = (overrides: Partial<GetUserInfoResponse> = {}): GetUserInfoResponse => ({
  id: 'user-1', username: 'someone', email: 'someone@example.com', roles: [],
  ...overrides,
})

const stateWithPermissions = (...permissions: string[]): AuthState => ({
  isAuthenticated: true,
  user: userInfo({ roles: [{ id: 'role-id', name: 'Admin', permissions: permissions.map((name) => ({ id: name, name, displayName: name })) }] }),
})
```

## What to cover

Worth a test — anything with a rule that would be expensive to get wrong and is testable without a
browser:

- **Security- and routing-shaped helpers**: redirect validation and the sign-in landing
  (`resolveSigninLanding`: the admin dashboard only for an account holding `Admin.Access`),
  `isAllowed`, `getMatchedAuthUrl` (including the "two entries match the same path" error and the
  `Admin.Access` fallback for an unlisted admin path), `isAuthRequired`/`isAnonymousUrl`,
  `decideGuardRedirect` (no session → sign in, missing permission → `/unauthorized`), and
  `proxy.test.ts` (public pages open with no cookie, `/admin` and `/account` redirect to sign-in).
  A new admin screen gets its `auth-urls.test.ts` case naming `Admin.Access` plus its own
  permission; a new area of the site gets its `proxy.test.ts` case.
- **Public-cache rules**: `decideRevalidation` in `lib/revalidate-rule.ts` (an unknown tag is a bad
  request, a tag the caller lacks the permission for is forbidden) — a new revalidatable tag gets its
  cases there; `i18n/dictionary-fetch.test.ts` pins that the anonymous dictionary is cached under the
  `localization` tag with no cookies and a signed-in one is fetched afresh.
- **SEO mapping**: `toMetadata`, `pageTitle`, `localizedPath`, `safeJsonLd` (`lib/seo/metadata.test.ts`),
  `toSitemap` and `toRobotsTxt` (`lib/seo/sitemap-robots.test.ts`) — see the `seo` skill.
- **Data shaping**: `getApiErrorMessages`, upload-limit arithmetic, export row mapping, form-value
  mappers such as `seo-page-form-values.ts` and `settings-mappers.ts`.
- **Reducers** — call `slice.reducer(state, action)` and assert on the state it returns.
- **Middleware** — run an action through `middleware(api)(next)(action)` with a recording `dispatch`
  and assert on what it dispatched, and on what it left alone.
- **Rules only the source can show**: `i18n/default-texts.test.ts` reads every web source file and
  fails when a literal-key translator call has no English default after `||`, or a plain-string
  default differs from `en.json`. Make such a scan prove it read something (a minimum file count, a
  file it must have seen), so a moved tree cannot pass by being absent.
- **Browser globals** a helper touches: stub `window` with `vi.stubGlobal` and
  `vi.unstubAllGlobals()` in `afterEach`.
- **Client/API mirrors**: `allow.test.ts` restates the backend constants and fails when `allow.ts`
  drifts — add the new constant there when you add a permission.
- **Locales**: `i18n/locales.test.ts` holds `i18nConfig.locales` equal to the backend's resource files;
  `i18n/locale-routing-loop.test.ts` replays proxy → API → guard hops and fails on any combination that
  does not settle (see `localization`). Key parity across locales is the backend's test, not this one.
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
