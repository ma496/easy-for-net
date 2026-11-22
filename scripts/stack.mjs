#!/usr/bin/env node
/**
 * The running app for a browser check: the API, `next dev` and the dev proxy, started detached
 * and stopped again as soon as the check is done.
 *
 *   pnpm stack up [--app <name>]      # start it (or hand back the one already running) and print its URLs
 *   pnpm stack status                 # what is recorded, and whether it still answers
 *   pnpm stack down                   # stop every process it started
 *
 * `pnpm dev` is a developer's foreground session on fixed ports. This is the same app for a review
 * inside a task, where nothing may be left running and nothing may collide with the developer's
 * own `pnpm dev`:
 *
 * - **Free ports.** Each of the three starts at its usual port and moves up past anything that
 *   already holds it (`nextFreePort`), so a stack never refuses to start and never answers in place
 *   of another app. The API's Web origins name the proxy's port, as `pnpm dev` arranges, so links and
 *   CORS agree with the address the browser opens.
 * - **Detached, and recorded.** Every process logs to `.agent-runs/stack-<proxy port>-<name>.log`,
 *   and `.agent-runs/stack.json` names their pids, ports and URLs. `up` started from a checkout whose
 *   stack is already running — alive, the same app, younger than AGENT_STACK_MAX_MINUTES (30) — prints
 *   that one instead of starting a second.
 * - **Ready means usable.** `up` returns only once the API's /health reports this checkout (the
 *   identity check verify uses) and the proxy serves a page from next dev, so the first browser call
 *   does not meet a 502. A first `next dev` compile is slow; the wait is up to 300 s.
 * - **A failure is written down.** When `up` cannot bring the app up it says why, exits 1, and writes
 *   `.agent-runs/stack-failure.json`, which `down` leaves in place: the runner reads it to tell a
 *   review that skipped the browser from one that had no app to open.
 *
 * PostgreSQL and Redis must already answer (the probes verify uses); this starts no containers.
 * When an API from this checkout is already running (the developer's `pnpm dev`), its build output
 * is locked on Windows, so this stack's API is built into `.agent-runs/stack-api-bin/` and run from
 * there — the task's current code, never the other API's older build. One `up` runs at a time per
 * checkout (`.agent-runs/stack.lock`); a second waits and then reuses the first one's stack.
 */
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, openSync, readdirSync, readFileSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import http from "node:http";
import { join } from "node:path";
import { apiBinDir, listProcesses, matchApiProcesses } from "./lib/api-process.mjs";
import { apiAdoptionVerdict } from "./lib/api-identity.mjs";
import { ensureDependencies, taskDependencies } from "./lib/dependencies.mjs";
import { DEV_PORTS, isPortFree, nextFreePort, readWebSettings, webOriginOverrides } from "./lib/dev-ports.mjs";
import {
  DEFAULT_STACK_MAX_MINUTES,
  STACK_FAILURE,
  STACK_LOCK,
  STACK_RECORD,
  lockVerdict,
  logTail,
  stackReuseVerdict,
  stackUrls,
  webAnswered,
  webEnv,
} from "./lib/dev-stack.mjs";
import { screenshotDir } from "./lib/mcp-connections.mjs";
import { isAlive, killTree, runSync, scriptArgs, spawnPortable } from "./lib/proc.mjs";
import { REPO_ROOT, appArg, config, positiveInt, selectApp } from "./lib/project-config.mjs";

const args = scriptArgs();
const command = args.find((a) => !a.startsWith("--") && a !== appArg(args)) ?? "status";
const RECORD = join(REPO_ROOT, STACK_RECORD);
const FAILURE = join(REPO_ROOT, STACK_FAILURE);
const RUNS = join(REPO_ROOT, ".agent-runs");
const API_WAIT_SECONDS = 300;
const WEB_WAIT_SECONDS = 300;
const MAX_MINUTES = positiveInt(process.env.AGENT_STACK_MAX_MINUTES, DEFAULT_STACK_MAX_MINUTES, "AGENT_STACK_MAX_MINUTES");

// A reader that stops early (`pnpm stack up | head`) closes the pipe; that is not a reason to
// abandon a half-started stack.
process.stdout.on("error", () => {});
const say = (message) => console.log(`stack: ${message}`);

switch (command) {
  case "up":
    await up();
    break;
  case "down":
    down();
    break;
  case "status":
    await status();
    break;
  default:
    console.error("usage: pnpm stack up|down|status [--app <name>]");
    process.exit(2);
}

/**
 * `up`, one at a time per checkout. A second call made while the first is still starting waits
 * for it and then finds its stack to reuse, rather than starting a stack of its own whose record
 * would overwrite the first's and leave that one where `down` cannot reach it. A lock whose
 * holder is gone is taken over. The lock is released however `up` ends, `process.exit` included.
 */
async function up() {
  mkdirSync(RUNS, { recursive: true });
  const lockPath = join(REPO_ROOT, STACK_LOCK);
  const deadline = Date.now() + (API_WAIT_SECONDS + WEB_WAIT_SECONDS + 120) * 1000;
  for (let said = false; ; ) {
    try {
      writeFileSync(lockPath, JSON.stringify({ pid: process.pid, at: new Date().toISOString() }), { flag: "wx" });
      break;
    } catch (err) {
      if (err.code !== "EEXIST") failUp(`cannot take ${STACK_LOCK}: ${err.message}`);
    }
    if (lockVerdict(readJson(lockPath), isAlive) === "take-over") {
      rmSync(lockPath, { force: true });
      continue;
    }
    if (Date.now() > deadline) failUp(`another \`stack up\` in this checkout has held ${STACK_LOCK} for too long.`);
    if (!said) say("another `stack up` in this checkout is starting the app — waiting for it.");
    said = true;
    await sleep(1000);
  }
  process.on("exit", () => {
    if (readJson(lockPath)?.pid === process.pid) rmSync(lockPath, { force: true });
  });
  await upLocked();
}

async function upLocked() {
  let app;
  try {
    app = selectApp(appArg(args));
  } catch (err) {
    failUp(err.message);
  }
  const repoRoot = realpathSync(REPO_ROOT);
  const recorded = readJson(RECORD);
  const verdict = stackReuseVerdict(recorded, { repoRoot, app: app.name, isAlive, maxMinutes: MAX_MINUTES });
  if (verdict.reuse) {
    say(`${verdict.reason}; reusing it.`);
    printUrls(recorded);
    return;
  }
  if (recorded) {
    say(`replacing the recorded stack: ${verdict.reason}.`);
    down({ quiet: true });
  }
  rmSync(FAILURE, { force: true });

  const stillDown = ensureDependencies(taskDependencies(config), { cwd: REPO_ROOT, log: say });
  if (stillDown.length > 0) {
    failUp(
      `${stillDown.map((d) => d.name).join(" and ")} ${stillDown.length === 1 ? "is" : "are"} not answering` +
        ` — ${stillDown.map((d) => d.whenMissing).filter(Boolean).join("; ") || "start them first"}.`,
    );
  }

  const ports = await pickPorts();
  const urls = stackUrls(app.devHosts, ports.proxy);
  mkdirSync(RUNS, { recursive: true });
  const record = { app: app.name, repoRoot, ports, pids: {}, urls, logs: {}, startedAt: new Date().toISOString() };
  const save = () => writeFileSync(RECORD, `${JSON.stringify(record, null, 2)}\n`);

  // A running API of this checkout (a developer's `pnpm dev`) holds its bin/ on Windows, so a
  // build there fails — and starting on that build would show the reviewers the backend as it was
  // before the task changed it. So the stack's API is then built into a folder of its own and run
  // from there: the task's current code, with the developer's API left alone.
  const binLocked = (() => {
    try {
      return matchApiProcesses(listProcesses(), apiBinDir(REPO_ROOT, app)).length > 0;
    } catch {
      return false;
    }
  })();
  const apiEnv = {
    PORT: String(ports.api),
    ...webOriginOverrides(readWebSettings({ root: REPO_ROOT, backend: app.backend }), ports.proxy),
  };
  if (binLocked) {
    say("an API of this checkout is already running, so this stack's API is built into a folder of its own.");
    startSeparateApi(record, app, apiEnv);
  } else {
    start(record, "api", "node", [join(REPO_ROOT, "scripts", "serve-api.mjs"), "--app", app.name], { cwd: REPO_ROOT, env: apiEnv });
  }
  start(record, "proxy", "node", [join(REPO_ROOT, "scripts", "dev-proxy.mjs"), "--port", String(ports.proxy), "--api", String(ports.api), "--web", String(ports.web)], {
    cwd: REPO_ROOT,
  });
  save();

  say(`waiting for the API on :${ports.api}…`);
  const api = await waitForApi(ports.api, repoRoot, () => isAlive(record.pids.api));
  if (!api.ok) failStarted(record, `the API did not come up: ${api.reason}`, "api");

  if (!existsSync(join(REPO_ROOT, app.web, "node_modules"))) {
    failStarted(record, `the web app's packages are not installed — run \`pnpm install\` in ${app.web}.`);
  }
  // next's own entry, started with node directly, for the reason `start` gives; `pnpm dev` only
  // when the package layout is not the one expected.
  const nextBin = join(REPO_ROOT, app.web, "node_modules", "next", "dist", "bin", "next");
  const [webCmd, webArgs] = existsSync(nextBin) ? ["node", [nextBin, "dev"]] : ["pnpm", ["dev"]];
  start(record, "web", webCmd, webArgs, {
    cwd: join(REPO_ROOT, app.web),
    env: webEnv({ webPort: ports.web, proxyPort: ports.proxy, apiPort: ports.api, env: process.env }),
  });
  save();

  say(`waiting for next dev behind the proxy on :${ports.proxy}…`);
  const host = new URL(urls[0]).host;
  const web = await waitForWeb(ports.proxy, host, { web: () => isAlive(record.pids.web), proxy: () => isAlive(record.pids.proxy) });
  if (!web.ok) failStarted(record, `the web app did not come up: ${web.reason}`, web.log ?? "web");

  say("the app is up.");
  printUrls(record);
}

/** One free port per process, each from its usual default upwards, never two the same. */
async function pickPorts() {
  const ports = {};
  const taken = [];
  for (const { key, fallback } of DEV_PORTS) {
    const free = (await isPortFree(fallback)) && !taken.includes(fallback) ? fallback : await nextFreePort(fallback, { taken });
    if (!free) failUp(`no free port near ${fallback} for the ${key}.`);
    ports[key] = free;
    taken.push(free);
  }
  return ports;
}

/**
 * Start one detached process, logging to its own file, and record its pid.
 *
 * Two things matter on Windows, where both were seen to fail. A process started through a shell
 * records the shell's pid, and the shell can die while the server it started runs on, leaving
 * `down` holding a dead pid and an orphan on the port. So `node` is started directly, never
 * through `cmd.exe`, and the recorded pid is the process that owns the tree. And a child still
 * attached to the caller's console is ended with that console when the command that ran
 * `stack up` returns, so every child is detached (`windowsHide` keeps its console out of sight);
 * on POSIX the same flag gives it its own process group, which is what `killTree` signals.
 */
function start(record, name, cmd, cmdArgs, { cwd, env = {} }) {
  const logPath = join(RUNS, `stack-${record.ports.proxy}-${name}.log`);
  const log = openSync(logPath, "w");
  const options = {
    cwd,
    detached: true,
    windowsHide: true,
    stdio: ["ignore", log, log],
    env: { ...process.env, EFN_APP: record.app, ...env },
  };
  const child = cmd === "node" ? spawn(process.execPath, cmdArgs, options) : spawnPortable(cmd, cmdArgs, options);
  child.unref();
  record.pids[name] = child.pid;
  record.logs[name] = logPath;
}

/**
 * Build the API into `.agent-runs/stack-api-bin/` and run it from there, for when the usual bin/
 * is held by another running API of this checkout. The build shares obj/ (which a running API
 * does not lock) and writes its output elsewhere; the host runs with the project folder as its
 * working directory, so its content root, settings and uploads are the ones `dotnet run` uses.
 */
function startSeparateApi(record, app, env) {
  const source = join(REPO_ROOT, app.backend, "Source");
  const project = readdirSync(source).find((f) => f.endsWith(".csproj"));
  if (!project) failUp(`no project file in ${source}.`);
  const out = join(RUNS, "stack-api-bin");
  const logPath = join(RUNS, `stack-${record.ports.proxy}-api.log`);
  say("building the API…");
  const build = runSync("dotnet", ["build", join(source, project), "-o", out, "--nologo", "-v", "quiet"], {
    cwd: REPO_ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });
  writeFileSync(logPath, `${build.stdout ?? ""}${build.stderr ?? ""}`);
  record.logs.api = logPath;
  if (build.status !== 0) failStarted(record, "the API did not build", "api");
  const log = openSync(logPath, "a");
  // dotnet is a real executable everywhere, so it is started directly and its pid is the host's.
  const child = spawn("dotnet", [join(out, project.replace(/\.csproj$/, ".dll")), "--urls", `http://localhost:${env.PORT}`], {
    cwd: source,
    detached: true,
    windowsHide: true,
    stdio: ["ignore", log, log],
    env: { ...process.env, EFN_APP: record.app, ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT || "Development", ...env },
  });
  child.unref();
  record.pids.api = child.pid;
}

/** Wait for /health to answer as this checkout, for as long as the API process lives. */
async function waitForApi(port, repoRoot, alive) {
  const deadline = Date.now() + API_WAIT_SECONDS * 1000;
  while (Date.now() < deadline) {
    if (!alive()) return { ok: false, reason: "its process exited" };
    try {
      const res = await fetch(`http://localhost:${port}/health`, { signal: AbortSignal.timeout(2000) });
      if (res.ok) {
        const verdict = apiAdoptionVerdict(await res.json().catch(() => null), repoRoot);
        return verdict.adopt ? { ok: true } : { ok: false, reason: `something else answered on :${port} — ${verdict.reason}` };
      }
    } catch {
      // Not listening yet.
    }
    await sleep(2000);
  }
  return { ok: false, reason: `no answer within ${API_WAIT_SECONDS} seconds` };
}

/**
 * Wait until the proxy serves a page from next dev. Asked on loopback with the dev host in the
 * Host header, since not every resolver maps `app.localhost` to loopback the way a browser does.
 */
async function waitForWeb(proxyPort, host, alive) {
  const deadline = Date.now() + WEB_WAIT_SECONDS * 1000;
  let last = "no answer";
  while (Date.now() < deadline) {
    // A proxy that could not bind its port exits at once; say so now rather than after the wait.
    if (!alive.proxy()) return { ok: false, reason: "the dev proxy exited", log: "proxy" };
    if (!alive.web()) return { ok: false, reason: "next dev exited", log: "web" };
    const status = await pageStatus(proxyPort, host, Math.max(1000, deadline - Date.now()));
    if (webAnswered(status)) return { ok: true };
    last = status ? `the proxy answered ${status}` : "no answer";
    await sleep(2000);
  }
  return { ok: false, reason: `${last} within ${WEB_WAIT_SECONDS} seconds` };
}

/** The status of `GET /` through the proxy, or 0 when nothing answered. */
function pageStatus(port, host, timeoutMs) {
  return new Promise((resolve) => {
    const req = http.get({ host: "127.0.0.1", port, path: "/", headers: { host } }, (res) => {
      res.resume();
      resolve(res.statusCode ?? 0);
    });
    // The first request triggers next dev's compile and can take a long time to answer.
    req.setTimeout(Math.min(timeoutMs, 120_000), () => req.destroy());
    req.on("error", () => resolve(0));
  });
}

/**
 * The addresses, and the folder this task's screenshots belong in. A screenshot given a bare file
 * name is written to the repository root, not to the MCP server's output folder, so a reviewer
 * passes the whole path: `<folder>/ui-users-375-dark.png`.
 */
function printUrls(record) {
  const branch = String(runSync("git", ["rev-parse", "--abbrev-ref", "HEAD"], { cwd: REPO_ROOT, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).stdout ?? "").trim();
  const shots = screenshotDir({ branch, taskId: process.env.AGENT_TASK_ID });
  console.log("");
  for (const url of record.urls ?? []) console.log(`  ${url}`);
  console.log("");
  console.log(`Save screenshots as ${shots}/<name>.png (browser_take_screenshot filename: the whole path).`);
  console.log("Sign in with the accounts in the \"Local sign-in\" table of CLAUDE.md.");
  console.log("Close the browser (browser_close) when the check is done, then run `pnpm stack down`.");
}

/** Stop every process the record names, and remove the record. */
function down({ quiet = false } = {}) {
  const record = readJson(RECORD);
  if (!record) {
    if (!quiet) say("no stack is running.");
    return;
  }
  for (const [name, pid] of Object.entries(record.pids ?? {})) {
    if (isAlive(pid)) killTree(pid);
    if (!quiet) say(`stopped ${name} (pid ${pid}).`);
  }
  rmSync(RECORD, { force: true });
}

async function status() {
  const record = readJson(RECORD);
  const failure = readJson(FAILURE);
  if (!record) {
    say("no stack is running.");
    if (failure) say(`the last \`up\` failed at ${failure.at}: ${failure.reason}`);
    return;
  }
  say(`${record.app}, started ${record.startedAt} from ${record.repoRoot}`);
  for (const [name, pid] of Object.entries(record.pids ?? {})) {
    say(`  ${name.padEnd(5)} :${record.ports?.[name]}  pid ${pid}  ${isAlive(pid) ? "running" : "not running"}`);
  }
  printUrls(record);
}

/** `up` failed before anything was started: write it down and exit. */
function failUp(reason) {
  writeFailure(reason);
  console.error(`stack: cannot start: ${reason}`);
  process.exit(1);
}

/** `up` failed after starting processes: say why, with the log that knows, then stop them. */
function failStarted(record, reason, logName) {
  let tail = "";
  if (logName && record.logs?.[logName]) {
    try {
      tail = logTail(readFileSync(record.logs[logName], "utf8"));
    } catch {
      /* no log to quote */
    }
  }
  writeFileSync(RECORD, `${JSON.stringify(record, null, 2)}\n`);
  down({ quiet: true });
  writeFailure(reason);
  console.error(`stack: cannot start: ${reason}`);
  if (tail) console.error(`\nThe last lines of ${record.logs[logName]}:\n${tail}`);
  process.exit(1);
}

function writeFailure(reason) {
  try {
    mkdirSync(RUNS, { recursive: true });
    writeFileSync(FAILURE, `${JSON.stringify({ reason, runId: process.env.AGENT_RUN_ID ?? null, at: new Date().toISOString() }, null, 2)}\n`);
  } catch {
    /* the message on stderr still says why */
  }
}

function readJson(path) {
  try {
    return existsSync(path) ? JSON.parse(readFileSync(path, "utf8")) : null;
  } catch {
    return null;
  }
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
