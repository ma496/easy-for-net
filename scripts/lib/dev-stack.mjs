/**
 * The pieces of a running app — API, `next dev`, dev proxy — that `pnpm dev` and `pnpm stack`
 * both need, and the record `pnpm stack` keeps of the one it started.
 *
 * `pnpm dev` is a developer's foreground session: fixed ports, every line on the terminal,
 * Ctrl+C stops it. `pnpm stack` is the same app for a browser check inside a task: started
 * detached on free ports, so it never collides with the developer's own `pnpm dev`, recorded in
 * `.agent-runs/stack.json`, and stopped again by `pnpm stack down` the moment the review that
 * needed it has reported. The environment each process is given is built here, once, so the two
 * cannot drift: a web app told a different API address than the proxy forwards to is a sign-in
 * that fails for no reason a reviewer could see.
 *
 * Pure functions except where a name says otherwise; the process side is in `scripts/stack.mjs`.
 */
import { normalizeRoot } from "./api-identity.mjs";

/** Where the record of the running stack lives, relative to the checkout. */
export const STACK_RECORD = ".agent-runs/stack.json";

/**
 * Where a failed `stack up` is written down. It outlives `stack down` on purpose: the runner
 * reads it after the session to tell "the reviewer skipped the browser" from "there was no app
 * to open", and only the second excuses a review read from code.
 */
export const STACK_FAILURE = ".agent-runs/stack-failure.json";

/** How long a stack may live before `up` replaces it rather than reusing it. */
export const DEFAULT_STACK_MAX_MINUTES = 30;

/**
 * The environment `next dev` is started with. The browser calls the API as the relative `/api`
 * on whatever host it opened (the proxy forwards it), and the Next.js server calls it directly on
 * loopback. A value already in `env` wins, as it does for `pnpm dev`.
 */
export function webEnv({ webPort, proxyPort, apiPort, env = {} }) {
  return {
    PORT: String(webPort),
    // The web app sends a page opened on next dev's own port to the same page through the proxy
    // (lib/utils/dev-proxy-redirect.ts): there /api reaches nothing and a sign-in fails.
    DEV_PROXY_PORT: String(proxyPort),
    NEXT_PUBLIC_API_URL: env.NEXT_PUBLIC_API_URL || "/api",
    API_INTERNAL_URL: env.API_INTERNAL_URL || `http://localhost:${apiPort}/api`,
  };
}

/** The addresses a browser opens: each of the app's dev hosts, on the proxy's port. */
export function stackUrls(devHosts, proxyPort) {
  const hosts = Array.isArray(devHosts) && devHosts.length > 0 ? devHosts : ["localhost"];
  return hosts.map((host) => `http://${host}:${proxyPort}`);
}

/**
 * Whether a recorded stack is one `up` may hand back instead of starting another: it was
 * started from this checkout, for this app, every process it names is alive, and it is younger
 * than `maxMinutes`. Returns the reason when it is not, for the log.
 *
 * @param {unknown} record  the parsed `.agent-runs/stack.json`, or null
 * @param {{ repoRoot: string, app: string, isAlive: (pid: number) => boolean, now?: number, maxMinutes?: number }} opts
 * @returns {{ reuse: boolean, reason: string }}
 */
export function stackReuseVerdict(record, { repoRoot, app, isAlive, now = Date.now(), maxMinutes = DEFAULT_STACK_MAX_MINUTES }) {
  if (!record || typeof record !== "object") return { reuse: false, reason: "no stack is recorded" };
  if (normalizeRoot(record.repoRoot) !== normalizeRoot(repoRoot)) {
    return { reuse: false, reason: `the recorded stack belongs to ${record.repoRoot}` };
  }
  if (record.app !== app) return { reuse: false, reason: `the recorded stack runs ${record.app}, not ${app}` };
  const pids = Object.values(record.pids ?? {});
  if (pids.length === 0 || !pids.every((pid) => Number.isInteger(pid) && isAlive(pid))) {
    return { reuse: false, reason: "a process of the recorded stack is no longer running" };
  }
  const started = Date.parse(record.startedAt);
  if (!Number.isFinite(started)) return { reuse: false, reason: "the recorded stack has no start time" };
  if (now - started > maxMinutes * 60_000) {
    return { reuse: false, reason: `the recorded stack is older than ${maxMinutes} minutes` };
  }
  return { reuse: true, reason: "a stack of this checkout is already running" };
}

/**
 * Whether the proxy's answer to a page means the web app is serving. A 502 is the proxy saying
 * an upstream is down (next dev still compiling, the API still building); anything below 500 —
 * the dashboard, or the redirect to sign-in — is the app answering.
 */
export function webAnswered(status) {
  return Number.isInteger(status) && status > 0 && status < 500;
}

/** The last `lines` lines of a log, for a failure message that says why without a second look. */
export function logTail(text, lines = 15) {
  return String(text ?? "").replace(/\r\n/g, "\n").trimEnd().split("\n").slice(-lines).join("\n");
}

/** The lock that keeps two `stack up` calls in one checkout from starting two stacks. */
export const STACK_LOCK = ".agent-runs/stack.lock";

/**
 * Whether `stack up` may take the lock another call left: only when that call is gone (its pid
 * dead, or the lock unreadable) — a live holder is waited for, and its stack reused once it is up.
 *
 * @param {unknown} lock  the parsed lock file, or null when it could not be read
 * @param {(pid: number) => boolean} isAlive
 * @returns {"wait" | "take-over"}
 */
export function lockVerdict(lock, isAlive) {
  const pid = lock && typeof lock === "object" ? lock.pid : null;
  return Number.isInteger(pid) && isAlive(pid) ? "wait" : "take-over";
}

/** Image files a screenshot can be saved as. */
const IMAGE = /\.(png|jpe?g|webp)$/i;

/**
 * Screenshots a browser check left at the repository root. A screenshot given a bare file name
 * is written there rather than into the task's folder, and the root is not ignored, so left alone
 * it would join the task's diff and its commit. Reads `git status --porcelain` lines and returns
 * the untracked image files directly at the root — nothing in a folder, nothing tracked.
 */
export function strayScreenshots(porcelain) {
  return String(porcelain ?? "")
    .split(/\r?\n/)
    .filter((line) => line.startsWith("?? "))
    .map((line) => line.slice(3).trim().replace(/^"|"$/g, ""))
    .filter((path) => path && !/[\/]/.test(path) && IMAGE.test(path));
}

/**
 * Where `pnpm dev` records itself while it runs, one file per launcher pid, relative to the checkout.
 * pnpm starts `dev.mjs` by a relative path, so its command line does not say which checkout it
 * belongs to; this record is how `stop-app.mjs` finds it.
 */
export const DEV_RUN_DIR = ".agent-runs";
export const devRunRecordName = (pid) => `dev-run-${pid}.json`;
export const DEV_RUN_RECORD = /^dev-run-\d+\.json$/;

/**
 * Whether a listed process (`{ pid, commandLine }`) is part of this checkout's running app as its
 * command line shows it: a dev launcher, dev proxy or API launcher started by an absolute path
 * into `<root>/scripts/`, or a `next dev` of one of the web apps (`webDirs`, relative to the
 * root). Paths are compared with forward slashes, and without regard to case on Windows. A
 * `next build`, another checkout's app and the caller itself (`selfPids`) are never matched.
 */
export function isAppProcessOf(p, { root, webDirs = [], windows = process.platform === "win32", selfPids = [] }) {
  if (!p?.pid || selfPids.includes(p.pid)) return false;
  const norm = (value) => {
    const s = String(value ?? "").replace(/\\/g, "/");
    return windows ? s.toLowerCase() : s;
  };
  const base = norm(root).replace(/\/+$/, "");
  if (!base) return false;
  const cmd = norm(p.commandLine);
  if (["dev.mjs", "dev-proxy.mjs", "serve-api.mjs"].some((name) => cmd.includes(`${base}/scripts/${name}`))) return true;
  const modules = webDirs.map((dir) => `${base}/${norm(dir).replace(/^\/+|\/+$/g, "")}/node_modules/`);
  if (!modules.some((m) => cmd.includes(m))) return false;
  return /\/next\/dist\/bin\/next["']?\s+dev\b/.test(cmd) || cmd.includes("/next/dist/server/lib/start-server.js");
}
