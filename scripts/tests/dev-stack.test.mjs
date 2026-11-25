import { strict as assert } from "node:assert";
import { test } from "node:test";

import { isAppProcessOf, lockVerdict, logTail, stackReuseVerdict, stackUrls, strayScreenshots, webAnswered, webEnv } from "../lib/dev-stack.mjs";
import { browserSection } from "../lib/build-record.mjs";

const NOW = Date.parse("2026-11-23T10:00:00Z");
const record = (overrides = {}) => ({
  app: "multi-tenant",
  repoRoot: "D:/repo",
  pids: { api: 10, proxy: 11, web: 12 },
  startedAt: "2026-11-23T09:50:00Z",
  ...overrides,
});
const alive = () => true;

test("webEnv points next dev at the proxy and the API, and a value already set wins", () => {
  assert.deepEqual(webEnv({ webPort: 3002, proxyPort: 3003, apiPort: 5001 }), {
    PORT: "3002",
    DEV_PROXY_PORT: "3003",
    NEXT_PUBLIC_API_URL: "/api",
    API_INTERNAL_URL: "http://localhost:5001/api",
  });
  assert.equal(webEnv({ webPort: 1, proxyPort: 2, apiPort: 3, env: { API_INTERNAL_URL: "http://api/api" } }).API_INTERNAL_URL, "http://api/api");
});

test("stackUrls names each dev host on the proxy's port", () => {
  assert.deepEqual(stackUrls(["app.localhost", "default.localhost"], 3005), ["http://app.localhost:3005", "http://default.localhost:3005"]);
  assert.deepEqual(stackUrls(undefined, 3000), ["http://localhost:3000"]);
});

test("a live, young stack of this checkout and app is reused", () => {
  assert.equal(stackReuseVerdict(record(), { repoRoot: "d:\\repo\\", app: "multi-tenant", isAlive: alive, now: NOW }).reuse, true);
});

test("a stack is replaced when anything about it no longer holds", () => {
  const opts = { repoRoot: "D:/repo", app: "multi-tenant", isAlive: alive, now: NOW };
  assert.equal(stackReuseVerdict(null, opts).reuse, false);
  assert.match(stackReuseVerdict(record({ repoRoot: "D:/other" }), opts).reason, /belongs to D:\/other/);
  assert.match(stackReuseVerdict(record({ app: "user-facing" }), opts).reason, /runs user-facing/);
  assert.match(stackReuseVerdict(record(), { ...opts, isAlive: (pid) => pid !== 12 }).reason, /no longer running/);
  assert.match(stackReuseVerdict(record({ pids: {} }), opts).reason, /no longer running/);
  assert.match(stackReuseVerdict(record({ startedAt: "2026-11-23T09:00:00Z" }), opts).reason, /older than 30 minutes/);
  assert.match(stackReuseVerdict(record({ startedAt: "soon" }), opts).reason, /no start time/);
});

test("a page answer means the app is up unless the proxy says an upstream is down", () => {
  assert.equal(webAnswered(200), true);
  assert.equal(webAnswered(307), true);
  assert.equal(webAnswered(404), true);
  assert.equal(webAnswered(502), false);
  assert.equal(webAnswered(0), false);
});

test("logTail keeps the last lines of a log", () => {
  assert.equal(logTail("a\r\nb\nc\n\n", 2), "b\nc");
});

test("the build record lists the browser notes and the task's screenshots", () => {
  const text = browserSection({ dir: ".playwright-mcp/main/s/01-t", screenshots: ["ui-users-375-dark.png"], notes: ["ui-ux-reviewer: rendered the page (1 screenshot(s))"] });
  assert.match(text, /^## Browser checks/);
  assert.match(text, /- ui-ux-reviewer: rendered the page/);
  assert.match(text, /in `\.playwright-mcp\/main\/s\/01-t\/`/);
  assert.match(text, /- `ui-users-375-dark\.png`/);
});

test("a task with no browser check gets no browser section", () => {
  assert.equal(browserSection(null), "");
  assert.equal(browserSection({ dir: "x", screenshots: [], notes: [] }), "");
});

test("a lock held by a running stack up is waited for; a dead or unreadable one is taken over", () => {
  assert.equal(lockVerdict({ pid: 42 }, (pid) => pid === 42), "wait");
  assert.equal(lockVerdict({ pid: 42 }, () => false), "take-over");
  assert.equal(lockVerdict(null, () => true), "take-over");
  assert.equal(lockVerdict({ pid: "42" }, () => true), "take-over");
});

test("only untracked images directly at the root count as stray screenshots", () => {
  const porcelain = [
    "?? ui-users-375-dark.png",
    "?? qa-signin.JPEG",
    '?? "page shot.webp"',
    "?? src/frontend/web/public/logo.png",
    " M README.md",
    "?? notes.md",
    "M  hero.png",
  ].join("\n");
  assert.deepEqual(strayScreenshots(porcelain), ["ui-users-375-dark.png", "qa-signin.JPEG", "page shot.webp"]);
  assert.deepEqual(strayScreenshots(""), []);
});

test("isAppProcessOf matches this checkout's dev launcher, proxy, API launcher and next dev, and nothing else", () => {
  const opts = { root: String.raw`D:\Repo`, webDirs: ["src/app/frontend/web"], windows: true, selfPids: [99] };
  const is = (commandLine, pid = 1) => isAppProcessOf({ pid, commandLine }, opts);
  // Started by an absolute path, as dev.mjs and stack.mjs start them; any case on Windows.
  assert.equal(is(String.raw`cmd.exe /d /s /c "node D:\Repo\scripts\dev-proxy.mjs --port 3000"`), true);
  assert.equal(is(String.raw`node d:\repo\scripts\serve-api.mjs`), true);
  assert.equal(is("node D:/Repo/scripts/dev.mjs --api-only"), true);
  // next dev through the pnpm shim (a `..` in the path) and the stack's direct start; not next build.
  assert.equal(is(String.raw`node "D:\Repo\src\app\frontend\web\node_modules\.bin\..\.pnpm\next@16\node_modules\next\dist\bin\next" dev`), true);
  assert.equal(is(String.raw`node D:\Repo\src\app\frontend\web\node_modules\next\dist\bin\next dev --port 3001`), true);
  assert.equal(is(String.raw`node D:\Repo\src\app\frontend\web\node_modules\next\dist\bin\next build`), false);
  // pnpm's relative start names no checkout, so only the launcher's own record can claim it.
  assert.equal(is("node  scripts/dev.mjs"), false);
  // Another checkout, a sibling whose name starts the same, the engine's own scripts, the caller.
  assert.equal(is(String.raw`node D:\Other\scripts\dev-proxy.mjs`), false);
  assert.equal(is(String.raw`node D:\Repo2\scripts\dev-proxy.mjs`), false);
  assert.equal(is(String.raw`node D:\Repo\scripts\agent-run.mjs`), false);
  assert.equal(is(String.raw`node D:\Repo\scripts\dev-proxy.mjs`, 99), false);
  // POSIX paths are case-sensitive.
  assert.equal(isAppProcessOf({ pid: 1, commandLine: "node /repo/scripts/dev-proxy.mjs" }, { root: "/repo", windows: false }), true);
  assert.equal(isAppProcessOf({ pid: 1, commandLine: "node /Repo/scripts/dev-proxy.mjs" }, { root: "/repo", windows: false }), false);
});
