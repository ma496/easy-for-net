#!/usr/bin/env node
/**
 * Stop the whole of this checkout's running app: the API, the web app and the dev proxy.
 *
 *   pnpm stop                          # stop everything of this checkout that runs
 *   pnpm stop --list                   # say what would be stopped, stop nothing
 *   node scripts/stop-app.mjs --quick  # what the hook runs: give up early when nothing can be running
 *
 * What it reaches:
 *
 *   - `pnpm dev` (scripts/dev.mjs, any of --api-only / --web-only) with everything it started,
 *     found through the record it keeps while it runs (`.agent-runs/dev-run-<pid>.json`): pnpm
 *     starts it by a relative path, so its command line does not name this checkout.
 *   - A dev proxy, `serve-api.mjs` or `next dev` of this checkout's web app, however started.
 *   - Any API running from an app's `Source/bin/` (the match `stop-api.mjs` uses).
 *   - The browser-check stack (`stack.mjs down`), when `.agent-runs/stack.json` records one.
 *
 * Listing every process with its command line costs about a second on Windows, and the hook
 * runs this after every backend edit. `--quick` therefore looks for cheap signs first — a live
 * `pnpm dev` record, a recorded stack, a live service verify left running (`--keep-stack`,
 * `.agent-runs/dev-service-<port>.json`), something listening on the dev ports (DEV_PROXY_PORT /
 * DEV_WEB_PORT / DEV_API_PORT) — and stops there when it finds none. An API started by hand on
 * another port is then missed; `pnpm stop` and the gate's `stop-api` still reach it.
 *
 * Only processes whose command line names this checkout are touched; another checkout's app, a
 * `next build` and the containers (PostgreSQL, Redis) are left alone. Called by
 * .claude/hooks/stop-api-on-backend-edit.mjs after a backend file changes.
 *
 * Prints `stop-app: stopped N process(es).` when anything was stopped, and exits 1 when something
 * survived being stopped (`stop-app: still running: <pids>`), since a surviving API's bin/ lock
 * fails the next build and it keeps serving the old code.
 */
import { existsSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { DEV_RUN_DIR, DEV_RUN_RECORD, STACK_RECORD, isAppProcessOf } from "./lib/dev-stack.mjs";
import { isPortFree, readDevPorts } from "./lib/dev-ports.mjs";
import { isAlive, killTree, runSync, sleepSync } from "./lib/proc.mjs";

// Resolved here rather than imported from project-config.mjs: that module asks git for the
// branches when it loads, which the quick path below has no use for.
const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const LIST_ONLY = process.argv.includes("--list") || process.argv.includes("--help");
const QUICK = process.argv.includes("--quick");

const devRuns = liveDevRuns();
const stackRecorded = existsSync(join(REPO_ROOT, STACK_RECORD));

if (QUICK && !devRuns.length && !stackRecorded && !liveServiceRecorded() && !(await anyDevPortListening())) {
  console.log("stop-app: nothing of this checkout is running.");
  process.exit(0);
}

const { APPS } = await import("./lib/project-config.mjs");
const { apiBinDir, dropDeadServiceRecords, listProcesses, matchApiProcesses } = await import("./lib/api-process.mjs");

/** pid → what it is, every process to stop, each once. */
const targets = new Map();
let listed = false;
try {
  const processes = listProcesses();
  listed = true;
  // A record is taken at its word only when its pid is still a dev launcher: a launcher that
  // crashed leaves its record behind, and the pid may since belong to something else entirely.
  for (const run of devRuns) {
    const p = processes.find((q) => q.pid === run.pid);
    if (p && /scripts[\\/]dev\.mjs/i.test(p.commandLine)) targets.set(run.pid, `pnpm dev (${run.app ?? "app"})`);
  }
  const webDirs = APPS.map((app) => app.web);
  for (const p of processes) {
    if (isAppProcessOf(p, { root: REPO_ROOT, webDirs, selfPids: [process.pid, process.ppid] })) {
      if (!targets.has(p.pid)) targets.set(p.pid, (p.commandLine || p.exe).slice(0, 200));
    }
  }
  for (const p of APPS.flatMap((app) => matchApiProcesses(processes, apiBinDir(REPO_ROOT, app)))) {
    if (!targets.has(p.pid)) targets.set(p.pid, (p.commandLine || p.exe).slice(0, 200));
  }
} catch (err) {
  console.log(`stop-app: ${err.message}; only the browser-check stack can be stopped.`);
}

for (const [pid, what] of targets) console.log(`stop-app: ${LIST_ONLY ? "would stop" : "stopping"} pid ${pid} — ${what}`);
if (LIST_ONLY) {
  if (stackRecorded) console.log("stop-app: would stop the browser-check stack (`pnpm stack down`).");
  if (!targets.size && !stackRecorded) console.log("stop-app: nothing of this checkout is running.");
  process.exit(0);
}

let stopped = 0;
for (const pid of targets.keys()) {
  if (isAlive(pid)) killTree(pid);
}
// taskkill and SIGTERM return before the process is gone, and its file handles go with it.
let survivors = [...targets.keys()];
for (let i = 0; i < 20 && survivors.some(isAlive); i++) sleepSync(250);
survivors = survivors.filter(isAlive);
stopped += targets.size - survivors.length;

if (stackRecorded) {
  const stack = runSync("node", [join(REPO_ROOT, "scripts", "stack.mjs"), "down"], { cwd: REPO_ROOT, encoding: "utf8", stdio: "pipe" });
  // `stack down` prints one `stack: stopped <name> (pid N).` line per process it had recorded;
  // one already stopped above is not counted twice.
  for (const [, pid] of `${stack.stdout ?? ""}`.matchAll(/stopped \S+ \(pid (\d+)\)/g)) {
    if (!targets.has(Number(pid))) stopped++;
  }
}

// A launcher killed outright never removes its own record; nor does one whose pid was reused.
for (const run of devRuns) if (!isAlive(run.pid) || (listed && !targets.has(run.pid))) rmSync(run.file, { force: true });
dropDeadServiceRecords(REPO_ROOT);

if (stopped) console.log(`stop-app: stopped ${stopped} process(es).`);
if (survivors.length) {
  console.log(`stop-app: still running: ${survivors.join(", ")}. Stop it by hand (\`pnpm stop\`) before building.`);
  process.exit(1);
}
if (!stopped) console.log("stop-app: nothing of this checkout was running.");

/** The `pnpm dev` launchers of this checkout that recorded themselves and are still alive. */
function liveDevRuns() {
  const dir = join(REPO_ROOT, DEV_RUN_DIR);
  if (!existsSync(dir)) return [];
  const runs = [];
  for (const name of readdirSync(dir).filter((f) => DEV_RUN_RECORD.test(f))) {
    const file = join(dir, name);
    try {
      const record = JSON.parse(readFileSync(file, "utf8"));
      if (Number.isInteger(record.pid) && isAlive(record.pid)) runs.push({ ...record, file });
      else rmSync(file, { force: true });
    } catch {
      // Half-written or unreadable: leave it for the launcher that owns it.
    }
  }
  return runs;
}

/**
 * Whether verify left an API of this checkout running (`--keep-stack`): it records each service in
 * `.agent-runs/dev-service-<port>.json`, on a port that need not be a dev port.
 */
function liveServiceRecorded() {
  const dir = join(REPO_ROOT, DEV_RUN_DIR);
  if (!existsSync(dir)) return false;
  return readdirSync(dir)
    .filter((f) => /^dev-service-\d+\.json$/.test(f))
    .some((name) => {
      try {
        const record = JSON.parse(readFileSync(join(dir, name), "utf8"));
        return Number.isInteger(record.pid) && isAlive(record.pid);
      } catch {
        return false;
      }
    });
}

/** Whether anything listens on one of the dev ports, as `pnpm dev` would have them. */
async function anyDevPortListening() {
  let ports;
  try {
    ports = readDevPorts({ root: REPO_ROOT });
  } catch {
    return true; // a port value that does not parse: look properly rather than guess
  }
  const taken = await Promise.all(["proxy", "web", "api"].map(async (key) => !(await isPortFree(ports[key], { timeoutMs: 200 }))));
  return taken.some(Boolean);
}
