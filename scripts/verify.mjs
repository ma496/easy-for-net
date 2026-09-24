#!/usr/bin/env node
/**
 * Scope-aware verification — the gate the autonomous loop actually trusts.
 *
 *   npm run verify                  # decide from the diff what needs checking, run it
 *   npm run verify -- --autostart   # bring the service up if a live check needs it
 *   npm run verify -- --json        # machine-readable summary for the runner
 *   npm run verify -- --scope working   # judge only uncommitted changes
 *
 * A static gate proves the code compiles, the unit suite passes, and everything builds.
 * For most products that is a weak signal on its own: a green gate says nothing about
 * whether the thing still works for a person. This looks at *what changed* and adds the
 * checks that change demands, so an unattended run cannot report success on behaviour it
 * never exercised.
 *
 * The contract is the exit code. Non-zero means do not ship. A required check that could
 * not run counts as a failure, not as a pass with a footnote — that is the whole point.
 *
 * What "the checks that change demands" means is `verify.checks` in `agentic.config.json`:
 * each entry names a command and the paths that make it necessary. A project with no live
 * checks configures none, and this becomes a wrapper around its gate.
 */
import { spawnSync } from "node:child_process";
import { mkdirSync, openSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import { createServer } from "node:net";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { apiAdoptionVerdict } from "./lib/api-identity.mjs";
import { IS_WINDOWS, killTree, runCommandSync, sleepSync, spawnCommand } from "./lib/proc.mjs";
import { compileRules, config, fill, WORK_BRANCH } from "./lib/project-config.mjs";

// Same reason as agent-run.mjs: cwd may hold only committed code, so the checks are driven
// from this file's own directory and run against cwd.
const SCRIPTS = dirname(fileURLToPath(import.meta.url));
const ROOT = join(SCRIPTS, "..");

const argv = process.argv.slice(2);
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

const SERVICE = config.verify.service;
const CHECKS = (config.verify.checks ?? []).map((c) => ({
  ...c,
  when: compileRules(c.when, `verify check "${c.name}"`),
}));

// Each concurrent run needs its own instance, or a live check validates whichever copy of
// the code happens to be serving rather than the one under test. The port is a
// *preference*, not a guarantee: if whatever answers there turns out to be serving some
// other checkout, this run moves to a free port and starts its own. Both are mutable for
// that reason.
let PORT = Number(argOf("port", SERVICE?.port ?? 3000));
// An explicit --api names one specific server. Relocating away from it would silently
// ignore the operator, so an identity mismatch there is a failure rather than a move.
const API_PINNED = argv.includes("--api");
let API = argOf("api", `http://localhost:${PORT}`).replace(/\/$/, "");

// The checkout being verified. Realpath-resolved because a service reports a resolved path
// and a checkout can be reached through a symlinked parent.
let CWD = process.cwd();
try {
  CWD = realpathSync(CWD);
} catch {
  /* keep the unresolved path; the adoption check reports the mismatch either way */
}
const BASE = argOf("base", WORK_BRANCH);
// "branch" asks what a pull request would contain — every commit on this branch plus the
// working tree. That is the blast radius the owner actually reviews, so it is the default.
// "working" narrows to uncommitted changes, for quick iteration on a long-lived branch.
const SCOPE = argOf("scope", "branch");
const JSON_OUT = flag("json");
const AUTOSTART = flag("autostart");
const KEEP_STACK = flag("keep-stack");

const say = (msg) => { if (!JSON_OUT) console.log(msg); };
const run = (cmd, args, opts = {}) =>
  spawnSync(cmd, args, { encoding: "utf8", stdio: JSON_OUT ? "pipe" : "inherit", ...opts });
const capture = (cmd, args) => spawnSync(cmd, args, { encoding: "utf8" }).stdout ?? "";
const runCommand = (command, opts = {}) => {
  if (!String(command ?? "").trim()) return { status: 0, skipped: true };
  return runCommandSync(command, { encoding: "utf8", stdio: JSON_OUT ? "pipe" : "inherit", ...opts });
};

// --- 1. what changed ---------------------------------------------------------------
// Committed work on this branch plus anything still in the working tree. An unattended
// run leaves its change uncommitted by design, so the second half is the usual case.
function changedPaths() {
  const paths = new Set();

  const mergeBase = SCOPE === "branch" ? capture("git", ["merge-base", "HEAD", BASE]).trim() : "";
  if (mergeBase) {
    for (const p of capture("git", ["diff", "--name-only", mergeBase]).split("\n")) {
      if (p.trim()) paths.add(p.trim());
    }
  }
  for (const line of capture("git", ["status", "--porcelain"]).split("\n")) {
    if (!line.trim()) continue;
    const p = line.slice(3).trim();
    paths.add((p.includes(" -> ") ? p.split(" -> ")[1] : p).replace(/^"|"$/g, ""));
  }
  return [...paths];
}

const changed = changedPaths();
/** Every configured check, with the files that demanded it. */
const demanded = CHECKS.map((check) => ({
  check,
  touched: changed.filter((p) => check.when.some((re) => re.test(p))),
})).filter((d) => d.touched.length > 0);

say(`Changed paths: ${changed.length}`);
for (const { check, touched } of demanded) {
  say(`  ${check.name}: ${touched.length} file(s) changed — this check is required`);
}
if (!changed.length) say("  (nothing changed — running the static gate only)");

// --- 2. the static gate, always -----------------------------------------------------
const results = [];
const record = (name, status, detail) => {
  results.push({ name, status, detail });
  say(`\n[${status.toUpperCase()}] ${name}${detail ? ` — ${detail}` : ""}`);
};

say(`\n=== gate: ${config.verify.gate} ===`);
const gate = runCommand(config.verify.gate);
if (gate.status !== 0) {
  record("gate", "fail", "the static gate failed");
  if (JSON_OUT) console.log(JSON.stringify({ ok: false, results, changed }, null, 2));
  process.exit(1);
}
record("gate", "pass");

// --- 3. live checks, if the change demands them --------------------------------------
const needsLive = demanded.some((d) => d.check.needsService);

/** The parsed health body, or null when nothing readable answered. */
async function serviceHealth(url) {
  try {
    const res = await fetch(`${url}${SERVICE?.healthPath ?? "/health"}`, {
      signal: AbortSignal.timeout(2000),
    });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  }
}

const verdictFor = (health) => apiAdoptionVerdict(health, CWD, SERVICE?.identityField ?? "repoRoot");

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
/** The service this run started, if it started one: `{ port, pid, url }`. */
let ownService = null;
const RUNS_DIR = join(CWD, ".agent-runs");
const recordPath = (port) => join(RUNS_DIR, `dev-service-${port}.json`);

/**
 * True when nothing already holds the port.
 *
 * Bound on every interface on purpose: a container publishes `*:PORT`, and a probe bound
 * only to 127.0.0.1 slips in beside it and reports the port free. This is still only a
 * hint — the identity re-check after startup is what actually stops a wrong adoption.
 */
function portFree(port) {
  return new Promise((resolve) => {
    const probe = createServer();
    probe.once("error", () => resolve(false));
    probe.listen(port, () => probe.close(() => resolve(true)));
  });
}

/** Ports to try, preferred one first, then upwards past whatever is squatting. */
async function freePorts(preferred, limit) {
  const found = [];
  for (let port = preferred; port < preferred + 60 && found.length < limit; port++) {
    if (await portFree(port)) found.push(port);
  }
  return found;
}

/**
 * Start this checkout's own service on `port` and wait for it to answer *as this checkout*.
 *
 * The identity re-check on the way up is not paranoia: two parallel runs can pick the same
 * free port, and the loser must not quietly measure the winner's code.
 *
 * @returns {Promise<"up" | "taken" | "timeout">} — "taken" is worth another port, a
 *   "timeout" is the service failing to boot and would time out again on the next one.
 */
async function startOwnService(port) {
  mkdirSync(RUNS_DIR, { recursive: true });
  const log = openSync(join(RUNS_DIR, `dev-service-${port}.log`), "a");
  // Its own process group on POSIX, so the whole process tree can be stopped again at the
  // end. Windows needs no group for that — killTree walks the tree — and a detached child
  // there would open a console window of its own.
  const child = spawnCommand(SERVICE.start, {
    cwd: CWD,
    env: { ...process.env, [SERVICE.portEnv ?? "PORT"]: String(port) },
    detached: !IS_WINDOWS,
    windowsHide: true,
    stdio: ["ignore", log, log],
  });
  child.unref();

  const url = `http://localhost:${port}`;
  // Written before it is known to be healthy: a run that dies mid-startup still leaves a
  // record, so the next run can tell its own leftovers from a stranger on the port.
  writeFileSync(
    recordPath(port),
    `${JSON.stringify({ port, pid: child.pid, repoRoot: CWD, startedAt: new Date().toISOString() }, null, 2)}\n`,
  );
  ownService = { port, pid: child.pid, url };

  const waitSeconds = Number(SERVICE.startTimeoutSeconds ?? 120);
  for (let i = 0; i < waitSeconds / 2; i++) {
    await sleep(2000);
    const health = await serviceHealth(url);
    if (!health) continue;
    const verdict = verdictFor(health);
    if (verdict.adopt) {
      say(`The service this run started is serving on :${port}.`);
      return "up";
    }
    say(`Something else took :${port} — ${verdict.reason}.`);
    return "taken";
  }
  say(
    `The service did not come up on :${port} within ${waitSeconds}s — see ` +
      `${join(RUNS_DIR, `dev-service-${port}.log`)}.`,
  );
  return "timeout";
}

/** Stop the service this run started, and drop its record. */
function stopOwnService() {
  if (!ownService) return;
  // The whole tree, so a child dies with its parent rather than outliving it.
  killTree(ownService.pid);
  rmSync(recordPath(ownService.port), { force: true });
  say(`Stopped the service this run started on :${ownService.port}.`);
  ownService = null;
}

/**
 * Bring up whatever the service depends on — a database, a queue, a cache.
 *
 * Shared by every run on this machine, and deliberately so: a dependency started per run
 * would give each one a different database and make the live checks disagree for reasons
 * that have nothing to do with the change. So: if it is already healthy, do nothing.
 */
function ensureDependencies() {
  for (const dep of SERVICE?.dependsOn ?? []) {
    const healthy = () =>
      dep.healthyWhen ? (runCommand(dep.healthyWhen, { stdio: "pipe" }).stdout ?? "").trim() !== "" : false;
    if (healthy()) {
      say(`${dep.name} is already up — starting the service only.`);
      continue;
    }
    say(`${dep.name} is down — starting it.`);
    const started = runCommand(dep.start, { cwd: ROOT });
    if (started.status !== 0) {
      liveFailure = `${dep.name} could not be started, so the required check(s) could not run.`;
      return false;
    }
    for (let i = 0; i < Number(dep.waitSeconds ?? 60) / 2 && !healthy(); i++) {
      sleepSync(2000);
    }
    if (!healthy()) {
      liveFailure = `${dep.name} did not become healthy, so the required check(s) could not run.`;
      return false;
    }
  }
  return true;
}

/**
 * Point the live checks at a service that is provably running *this* checkout.
 *
 * Returning the moment anything answers on the port is how a checkout's live check comes to
 * report PASS while exercising some other code: a container publishes the same port, and a
 * stale dev server from an earlier run does the same thing. So an already-running service is
 * adopted only when it reports this directory; otherwise this run starts its own on a free
 * port, and if it cannot, the live check fails. Sets `liveFailure` with the reason.
 */
let liveFailure = "";

async function ensureStack() {
  if (!SERVICE) {
    liveFailure =
      "a check needs the app running, but `verify.service` is not configured in " +
      "agentic.config.json, so nothing knows how to start it.";
    return false;
  }

  const existing = await serviceHealth(API);
  const verdict = existing ? verdictFor(existing) : null;

  if (verdict?.adopt) {
    say(`\nUsing the service already serving at ${API} — ${verdict.reason}.`);
    return true;
  }
  if (verdict) {
    say(`\nRefusing the service at ${API}: ${verdict.reason}.`);
    if (API_PINNED) {
      liveFailure =
        `the service at ${API} cannot be tied to this checkout — ${verdict.reason}. ` +
        "--api pins this run to that server, so the check would have measured other code. " +
        "Point --api at this checkout's service, or drop --api and let this run start one.";
      return false;
    }
  }

  if (!AUTOSTART) {
    liveFailure = verdict
      ? `the service at ${API} cannot be tied to this checkout — ${verdict.reason}. The required ` +
        "check(s) would have measured other code. Start this checkout's service or pass --autostart."
      : `nothing is serving at ${API}, so the required check(s) could not run. ` +
        "Start the app or pass --autostart.";
    return false;
  }

  say("\nNo service for this checkout is serving and a live check is required — starting one.");
  if (!ensureDependencies()) return false;

  // Prefer the requested port; step past it when something else already holds it, rather
  // than racing for a bind that is never going to succeed. A foreign service that just
  // answered there is proof enough that the port is taken, whatever the probe says.
  const candidates = await freePorts(existing ? PORT + 1 : PORT, 3);
  if (candidates.length === 0) {
    liveFailure = `no free port was found near :${PORT} to start this checkout's service on.`;
    return false;
  }
  const tried = [];
  for (const port of candidates) {
    tried.push(port);
    const outcome = await startOwnService(port);
    if (outcome === "up") {
      PORT = port;
      API = `http://localhost:${port}`;
      return true;
    }
    stopOwnService();
    if (outcome === "timeout") {
      liveFailure =
        `this checkout's service did not come up on :${port}, so the required check(s) ` +
        `could not run. See ${join(RUNS_DIR, `dev-service-${port}.log`)}.`;
      return false;
    }
    // "taken" — another run won the race for that port. Try the next free one.
  }
  liveFailure = `this checkout's service could not be started on any of :${tried.join(", :")}.`;
  return false;
}

if (needsLive) {
  const live = await ensureStack();

  if (!live) {
    // A required check that could not run is a failure — and so is one that could only be
    // run against somebody else's code. Reporting "verified" for either is exactly the lie
    // this script exists to prevent.
    record("live checks", "fail", liveFailure);
    stopOwnService();
    if (JSON_OUT) console.log(JSON.stringify({ ok: false, results, changed }, null, 2));
    process.exit(1);
  }
}

for (const { check } of demanded) {
  say(`\n=== ${check.name}${check.description ? `: ${check.description}` : ""} ===`);
  const outcome = runCommand(fill(check.run, { api: API, port: PORT }));
  record(check.name, outcome.status === 0 ? "pass" : "fail");
}

// Every run that starts a service on a fresh port would otherwise strand it, and the next
// run — on a different checkout — could adopt it. It cannot any more, but a growing pile of
// orphaned servers holding ports is its own problem, so stop what this run started.
// `--keep-stack` leaves it up for inspection and keeps the record file that names its port,
// pid, and checkout, so a later run can tell a leftover of ours from a stranger.
if (ownService && !KEEP_STACK) {
  stopOwnService();
} else if (ownService) {
  say(
    `\nLeaving the service this run started on :${ownService.port} (pid ${ownService.pid}), recorded in ` +
      `${recordPath(ownService.port)}. Stop it with \`${IS_WINDOWS ? `taskkill /PID ${ownService.pid} /T /F` : `kill -- -${ownService.pid}`}\`.`,
  );
}

// --- 4. verdict --------------------------------------------------------------------------
const failed = results.filter((r) => r.status === "fail");
const demandedNames = new Set(demanded.map((d) => d.check.name));
const skipped = CHECKS.filter((c) => !demandedNames.has(c.name)).map(
  (c) => `${c.name} (${c.skipNote ?? "the paths it watches were untouched"})`,
);

if (JSON_OUT) {
  console.log(JSON.stringify({ ok: failed.length === 0, results, skipped, changed }, null, 2));
} else {
  console.log(`\n${"=".repeat(70)}`);
  for (const r of results) console.log(`  ${r.status === "pass" ? "✓" : "✗"} ${r.name}`);
  for (const s of skipped) console.log(`  – skipped: ${s}`);
  console.log("=".repeat(70));
  console.log(failed.length === 0 ? "Verified." : `FAILED: ${failed.map((f) => f.name).join(", ")}`);
}

process.exit(failed.length === 0 ? 0 : 1);
