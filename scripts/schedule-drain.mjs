#!/usr/bin/env node
/**
 * The trigger — what makes the loop start without anybody typing a command.
 *
 *   npm run schedule -- install                 # drain now, then every minute
 *   npm run schedule -- install --interval 900  # every 15 minutes
 *   npm run schedule -- install --max-usd-per-task 80 --max-usd-per-drain 300
 *   npm run schedule -- install --auto-push --no-refuse-dirty
 *   npm run schedule -- status
 *   npm run schedule -- uninstall
 *
 * Installs a timer that runs `npm run loop` on an interval — observe the running product,
 * file specs for what is failing, then drain the queue. On Windows that is a Task Scheduler
 * task, on macOS a launchd agent; on Linux it prints the equivalent cron line. An empty
 * queue exits immediately and costs nothing, so a frequent interval is cheap; the expense
 * is a queued task, and queueing one is a deliberate act.
 *
 * What the scheduled run may do: pick up tasks, build them one at a time on the work branch,
 * verify them, commit each before the next starts, and record the outcome. Committing is what
 * lets the next task build on the last one, so an unattended run has to do it.
 *
 * What it may NOT do: merge. Pushing is off unless `--auto-push` (or `AGENT_AUTO_PUSH=1`) is
 * given at install. `--model` (or `AGENT_MODEL`) sets the model each task's own session runs
 * on. Pass `--no-refuse-dirty` (or `AGENT_REFUSE_DIRTY_START=0`) to park dirty WIP instead of
 * refusing the drain — the default refuses so a person's edits are not moved aside.
 *
 * Every flag and ceiling is written into what the scheduler runs, because a scheduled run
 * inherits no shell of yours. Re-run install after changing any of them.
 */
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import { homedir, platform } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { wantsAutoPush, wantsRefuseDirtyStart } from "./lib/agent-flags.mjs";
import {
  DEFAULT_MAX_USD_PER_DRAIN,
  DEFAULT_MAX_USD_PER_TASK,
  formatCeiling,
  parseCeiling,
} from "./lib/budget.mjs";
import { isAlive, killTree } from "./lib/proc.mjs";
import { config, WORK_BRANCH } from "./lib/project-config.mjs";
import {
  launchdLabel,
  posixCommand,
  scheduleEnv,
  schtasksCreateArgs,
  windowsLauncher,
  windowsTaskName,
  windowsWrapper,
} from "./lib/schedule.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const LOG_DIR = join(ROOT, ".agent-runs");
const LOG_FILE = join(LOG_DIR, "drain.log");
const LOCK = join(ROOT, ".agent-queue", "drain.lock");
const OS = platform();

const argv = process.argv.slice(2);
const action = argv.find((a) => !a.startsWith("--")) ?? "status";
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

if (!["install", "status", "uninstall"].includes(action)) {
  console.error(`Unknown action "${action}". Use: install | status | uninstall`);
  process.exit(1);
}

// --- the platform's scheduler -------------------------------------------------------------

/** Task Scheduler (Windows). The batch wrapper and its windowless launcher live in .agent-runs/. */
const windows = {
  name: windowsTaskName(config.project.name),
  wrapper: join(LOG_DIR, "schedule-drain.cmd"),
  launcher: join(LOG_DIR, "schedule-drain.vbs"),
  schtasks: (...args) => spawnSync("schtasks", args, { encoding: "utf8", windowsHide: true }),
  status() {
    const q = this.schtasks("/Query", "/TN", this.name, "/V", "/FO", "LIST");
    if (q.status !== 0) return console.log(`task:      ${this.name} — not installed`);
    const field = (label) => q.stdout.match(new RegExp(`^${label}:\\s*(.+)$`, "m"))?.[1]?.trim();
    console.log(`task:      ${this.name} (${field("Status") ?? "unknown"})`);
    console.log(`last run:  ${field("Last Run Time") ?? "never"}  (result ${field("Last Result") ?? "?"})`);
    console.log(`next run:  ${field("Next Run Time") ?? "unknown"}`);
  },
  uninstall() {
    this.schtasks("/Delete", "/TN", this.name, "/F");
    for (const f of [this.wrapper, this.launcher]) if (existsSync(f)) unlinkSync(f);
    return this.name;
  },
  install(env, interval) {
    writeFileSync(this.wrapper, windowsWrapper(ROOT, env, LOG_FILE));
    writeFileSync(this.launcher, windowsLauncher(this.wrapper));
    const created = this.schtasks(...schtasksCreateArgs(this.name, interval, this.launcher));
    if (created.status !== 0) {
      console.error(`Task Scheduler refused to create ${this.name}:`);
      console.error((created.stderr || created.stdout).trim());
      process.exit(1);
    }
    // Start now rather than waiting out a first interval.
    this.schtasks("/Run", "/TN", this.name);
    return this.name;
  },
};

/** launchd (macOS). */
const mac = {
  name: launchdLabel(config.project.name),
  get plist() {
    return join(homedir(), "Library", "LaunchAgents", `${this.name}.plist`);
  },
  launchctl: (...args) => spawnSync("launchctl", args, { encoding: "utf8" }),
  get domain() {
    return `gui/${process.getuid()}`;
  },
  status() {
    console.log(`plist:     ${existsSync(this.plist) ? this.plist : "not installed"}`);
    const list = this.launchctl("print", `${this.domain}/${this.name}`);
    if (list.status !== 0) return console.log("launchd:   not loaded");
    const state = list.stdout.match(/state = (\w+)/)?.[1] ?? "unknown";
    const every = list.stdout.match(/run interval = (\d+)/)?.[1];
    console.log(`launchd:   loaded (state ${state}${every ? `, every ${every}s` : ""})`);
  },
  uninstall() {
    this.launchctl("bootout", `${this.domain}/${this.name}`);
    if (existsSync(this.plist)) unlinkSync(this.plist);
    return this.name;
  },
  install(env, interval) {
    // A login shell, because launchd starts with a minimal PATH and node usually comes from
    // a package manager that only a login shell puts on the path.
    const command = posixCommand(ROOT, env).replace(/&/g, "&amp;").replace(/</g, "&lt;");
    const plist = `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>${this.name}</string>
  <key>ProgramArguments</key>
  <array>
    <string>/bin/zsh</string>
    <string>-lc</string>
    <string>${command}</string>
  </array>
  <key>StartInterval</key>
  <integer>${interval}</integer>
  <key>RunAtLoad</key>
  <true/>
  <key>StandardOutPath</key>
  <string>${LOG_FILE}</string>
  <key>StandardErrorPath</key>
  <string>${LOG_FILE}</string>
  <key>WorkingDirectory</key>
  <string>${ROOT}</string>
</dict>
</plist>
`;
    mkdirSync(dirname(this.plist), { recursive: true });
    writeFileSync(this.plist, plist);
    this.launchctl("bootout", `${this.domain}/${this.name}`); // not loaded yet is fine
    const loaded = this.launchctl("bootstrap", this.domain, this.plist);
    if (loaded.status !== 0) {
      console.error(`Wrote ${this.plist} but launchctl refused to load it:`);
      console.error(loaded.stderr.trim() || loaded.stdout.trim());
      process.exit(1);
    }
    return this.name;
  },
};

const scheduler = OS === "win32" ? windows : OS === "darwin" ? mac : null;

// --- options, shared by every platform ------------------------------------------------------

// One minute. The interval is not how often work happens — one drain runs the whole queue —
// it is how long the machine waits after a drain *stops*, and a drain stops on every block,
// every empty queue and every dependency wall. Nothing about a short interval is expensive:
// the scheduler will not start a second copy while one is running, the queue's own lock
// refuses one anyway, planning is keyed on a content hash so an unchanged spec costs no model
// call, and a drain with nothing runnable exits in seconds.
const interval = Number(argOf("interval", "60"));

/** Resolved through parseCeiling so what is scheduled is always an explicit, valid value. */
const maxUsdPerTask = parseCeiling(argOf("max-usd-per-task", process.env.AGENT_MAX_USD_PER_TASK), DEFAULT_MAX_USD_PER_TASK);
const maxUsdPerDrain = parseCeiling(argOf("max-usd-per-drain", process.env.AGENT_MAX_USD_PER_DRAIN), DEFAULT_MAX_USD_PER_DRAIN);
/** `off` round-trips back through parseCeiling as Infinity, so no ceiling stays no ceiling. */
const ceilingArg = (usd) => (Number.isFinite(usd) ? String(usd) : "off");

const autoPush = argv.includes("--no-auto-push") ? false : argv.includes("--auto-push") ? true : wantsAutoPush();
const refuseDirty = argv.includes("--no-refuse-dirty")
  ? false
  : argv.includes("--refuse-dirty")
    ? true
    : wantsRefuseDirtyStart();

const env = scheduleEnv({
  model: argOf("model", process.env.AGENT_MODEL ?? config.budget.model ?? "opus"),
  maxUsdPerTask: ceilingArg(maxUsdPerTask),
  maxUsdPerDrain: ceilingArg(maxUsdPerDrain),
  // How many times one brief may be started before it goes to failed/ for a person — it
  // matters most here, where a task the timer keeps restarting is the thing nobody watches.
  maxRunsPerTask: argOf("max-runs-per-task", process.env.AGENT_MAX_RUNS_PER_TASK ?? "3"),
  autoPush,
  refuseDirty,
});

// --- actions --------------------------------------------------------------------------------

if (!scheduler) {
  console.error(`No scheduler is supported on ${OS}. Run the loop from cron instead:\n`);
  console.error(`  * * * * * ${posixCommand(ROOT, env)} >> .agent-runs/drain.log 2>&1`);
  process.exit(action === "status" ? 0 : 1);
}

if (action === "status") {
  scheduler.status();
  console.log(`log:       ${LOG_FILE}`);
  console.log(`\nQueue right now:`);
  spawnSync("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"], { stdio: "inherit" });
  process.exit(0);
}

if (action === "uninstall") {
  const name = scheduler.uninstall();

  // Unscheduling does not stop a drain that is already running, and a drain's descendants
  // outlive the scheduler's own job: an agent-run mid-task keeps its claude session and keeps
  // editing files with nobody expecting it to. "Uninstall" has to mean stopped, so the drain
  // holding the queue lock is stopped with everything it started.
  const holder = existsSync(LOCK) ? Number(readFileSync(LOCK, "utf8").trim()) : 0;
  if (holder && holder !== process.pid && isAlive(holder)) {
    console.log(`Stopping the drain in flight (pid ${holder}) and everything it started…`);
    killTree(holder);
    console.log(
      "A task interrupted mid-run leaves its partial edits in the working tree. They are\n" +
        "NOT committed and NOT reverted here — review them, then either keep them or run\n" +
        "`git checkout -- <paths>`, and `npm run queue -- resume` to requeue the task.",
    );
  }

  console.log(`Removed ${name}. Nothing is scheduled any more.`);
  process.exit(0);
}

mkdirSync(LOG_DIR, { recursive: true });
const name = scheduler.install(env, interval);

console.log(`Scheduled: ${name}`);
console.log(`  every ${Math.max(60, interval)}s, building on ${config.project.branch || `the checked-out branch (now ${WORK_BRANCH})`}`);
console.log(`  spend ceilings: ${formatCeiling(maxUsdPerTask)} per task, ${formatCeiling(maxUsdPerDrain)} per drain`);
console.log(`  model: ${env.AGENT_MODEL}`);
console.log(autoPush ? "  AGENT_AUTO_PUSH=1 — each verified commit is pushed" : "  AGENT_AUTO_PUSH=0 — commits stay local");
console.log(
  refuseDirty
    ? "  AGENT_REFUSE_DIRTY_START=1 — a dirty tree stops the drain instead of parking WIP"
    : "  AGENT_REFUSE_DIRTY_START=0 — dirty WIP is parked under .agent-runs/interrupted/",
);
console.log(`  log:  ${LOG_FILE}`);
console.log(`\nQueue work with:   npm run queue -- add "<task>"`);
console.log(`Check on it with:  npm run schedule -- status`);
console.log(`Stop it with:      npm run schedule -- uninstall`);
console.log("\nFlags and ceilings are baked into the scheduled command. Re-run install after changing them.");
