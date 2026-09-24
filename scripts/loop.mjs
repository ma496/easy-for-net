#!/usr/bin/env node
/**
 * One command for the whole cycle.
 *
 *   npm run loop                    # look, plan, build, report
 *   npm run loop -- --dry-run       # say what it would do, change nothing
 *
 * Everything else in scripts/ does one job. This is the one you run when you do not want
 * to think about which job that is: it works out where things stand, asks the running
 * product whether anything is broken, turns whatever is waiting into tasks, builds the
 * ones whose dependencies are met, verifies them, commits each to the work branch, and
 * finishes by telling you exactly what needs a human — which is now only reviewing and pushing.
 *
 * Tasks are built on the work branch (`project.branch`, or the one checked out), one at a time, each committed before the next starts. That is
 * what lets a `Depends-on:` chain finish in a single run instead of stalling until someone
 * merges. Nothing here pushes or merges — those stay yours.
 */
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { BUDGET_EXIT_CODE } from "./lib/budget.mjs";
import { runCommandSync, sleepSync } from "./lib/proc.mjs";
import { config, WORK_BRANCH } from "./lib/project-config.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const argv = process.argv.slice(2);
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

// Three, on measurement. It was cut to two on the argument that third attempts were waste;
// the journal now has 35 landed runs instead of the 27 that argument was written from, and
// 14 of them landed on attempt three — 40% of everything the loop has ever shipped. Of the
// five with per-attempt turns recorded, none had an empty first attempt, so those are three
// real builds, not a salvage plus two. Cutting to two does not save the third attempt's
// money: the task fails, goes to failed/, and is requeued to start again from nothing.
const ATTEMPTS = argOf("attempts", String(config.budget.attempts));
const DRY = flag("dry-run");

const run = (cmd, args, opts = {}) =>
  spawnSync(cmd, args, { cwd: ROOT, stdio: "inherit", encoding: "utf8", ...opts });
const capture = (cmd, args) =>
  spawnSync(cmd, args, { cwd: ROOT, encoding: "utf8" }).stdout ?? "";

const rule = (title) => console.log(`\n${"─".repeat(72)}\n  ${title}\n${"─".repeat(72)}`);

// --- 1. where things stand ------------------------------------------------------------
rule("1 · Where things stand");

const branch = capture("git", ["rev-parse", "--abbrev-ref", "HEAD"]).trim();
// Anything uncommitted outside .agent-queue/ stops a drain dead — and the refusal goes to
// a log nobody is reading, so from the outside the timer simply appears to do nothing. It
// is the single most likely reason an unattended loop silently stops making progress, so
// it is stated here at the top and again at the bottom rather than left to be discovered.
const dirtyPaths = capture("git", ["status", "--porcelain"])
  .split("\n")
  .filter((l) => l.trim() && !l.slice(3).startsWith(".agent-queue/"));
console.log(`Branch: ${branch}   ·   uncommitted files: ${dirtyPaths.length}`);
if (dirtyPaths.length > 0) {
  console.log("\n  ⚠  The working tree is dirty, so NO TASK CAN BUILD this cycle.");
  console.log("     Each task starts from a clean tree so its commit holds only its own work.");
  for (const p of dirtyPaths.slice(0, 8)) console.log(`       ${p}`);
  if (dirtyPaths.length > 8) console.log(`       …and ${dirtyPaths.length - 8} more`);
  console.log("     Commit or stash them and the next cycle picks up where this one stopped.");
}

/**
 * Whatever the cycle needs running before it can look at anything — a database, a queue,
 * a container. After a reboot none of it is up, and waiting for a person to type the start
 * command defeats the point of a timer, so bring it up here.
 *
 * A dependency that cannot be started is said plainly rather than pretended away: planning
 * and the static gate still work without it, and a cycle that does what it can beats one
 * that refuses.
 */
const preflight = config.cycle.preflight ?? [];
const readyServices = new Set();

const probe = (command) => {
  if (!command) return true;
  const out = runCommandSync(command, { cwd: ROOT, encoding: "utf8" });
  return (out.stdout ?? "").trim() !== "" || (out.status === 0 && !command.includes("--filter"));
};

for (const service of preflight) {
  if (probe(service.healthyWhen)) {
    console.log(`${service.name}: up`);
    readyServices.add(service.name);
    continue;
  }
  if (DRY) {
    console.log(`${service.name}: DOWN (dry run — not starting it)`);
    continue;
  }
  if (!service.start) {
    console.log(`${service.name}: down — ${service.whenMissing ?? "this cycle runs without it"}`);
    continue;
  }
  process.stdout.write(`${service.name}: down — starting it`);
  runCommandSync(service.start, { cwd: ROOT, stdio: "ignore" });
  let up = false;
  for (let i = 0; i < Number(service.waitSeconds ?? 90) / 2 && !up; i += 1) {
    up = probe(service.healthyWhen);
    if (!up) {
      process.stdout.write(".");
      sleepSync(2000);
    }
  }
  console.log(up ? " up" : ` gave up — ${service.whenMissing ?? "this cycle runs without it"}`);
  if (up) readyServices.add(service.name);
}

run("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"]);

// Unpushed commits on the work branch are the state that matters now: that is where finished work
// lives until the owner pushes it.
const aheadOf = (ref) => (capture("git", ["rev-list", "--count", `${ref}..HEAD`]) ?? "").trim();
const ahead = branch === WORK_BRANCH ? aheadOf(`origin/${WORK_BRANCH}`) : "";
if (ahead && ahead !== "0") {
  console.log(
    `\n${ahead} commit(s) on ${WORK_BRANCH} not yet pushed — review with ` +
      `\`git log --oneline origin/${WORK_BRANCH}..HEAD\`.`,
  );
}

// --- 2. ask the product what is broken --------------------------------------------------
rule("2 · What production says is broken");
// Optional, and off until a project writes one: a command that reads the running product —
// its logs, its error rates, its unhappy users — and files a brief into specs/ when a
// pattern crosses a threshold. It files a problem, never a solution.
const observeCommand = config.cycle.observe;
const observeBlockedBy = (config.cycle.observeNeeds ?? []).filter((n) => !readyServices.has(n));
if (!observeCommand) {
  console.log("No observe command is configured (`cycle.observe` in agentic.config.json).");
} else if (observeBlockedBy.length > 0) {
  console.log(`Skipped — ${observeBlockedBy.join(", ")} is not running.`);
} else {
  runCommandSync(`${observeCommand}${DRY ? " --dry-run" : ""}`, { cwd: ROOT, stdio: "inherit" });
}

// --- 3 & 4. plan and build ---------------------------------------------------------------
rule("3 · Plan new specs, then build what is ready");
let drainStatus = 0;
if (DRY) {
  console.log("Dry run — planning and building skipped.");
  run("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"]);
} else {
  // The drain's exit status is read, not discarded. It used to be ignored entirely, which
  // meant a drain that stopped on its spend ceiling looked exactly like one that ran out of
  // work — and on a timer, where nobody watches the log, "the queue is empty" and "the
  // budget is gone" are the two conclusions it is worst to confuse.
  drainStatus =
    run("node", [
      join(ROOT, "scripts", "agent-queue.mjs"),
      "drain",
      "--attempts",
      ATTEMPTS,
    ]).status ?? 0;
}

// --- 5. what needs a human ----------------------------------------------------------------
rule("4 · What needs you");

// The failed lane used to be invisible here, which made it a place tasks went to be
// forgotten: nothing retried them, nothing escalated them, and the loop report did not
// mention them at all. A task can now stop making progress without that going unsaid.
if (dirtyPaths.length > 0) {
  console.log(`${dirtyPaths.length} uncommitted file(s) blocked every task this cycle.`);
  console.log("Commit or stash them; nothing else here can proceed until you do.\n");
}

// A cycle that stopped because it had spent its ceiling has work left that nothing is going
// to do on its own schedule any faster — the next fire starts a fresh budget, but if the
// queue is bigger than one cycle's worth of money, that is a decision for a person.
if (drainStatus === BUDGET_EXIT_CODE) {
  const waiting = existsSync(join(ROOT, ".agent-queue", "todo"))
    ? readdirSync(join(ROOT, ".agent-queue", "todo")).filter((f) => f.endsWith(".md")).length
    : 0;
  console.log("This cycle stopped on its spend ceiling, not because the queue was empty.");
  console.log(
    `${waiting} task(s) are still in todo/, untouched. The next scheduled run starts a fresh\n` +
      "budget and picks them up. To let one run go further, raise AGENT_MAX_USD_PER_DRAIN\n" +
      "(and AGENT_MAX_USD_PER_TASK for a single expensive task) — the timer passes both\n" +
      "through, so `npm run schedule -- install` again after changing them.\n",
  );
  console.log("  What each run cost:  npm run auto:status\n");
}

const failedTasks = existsSync(join(ROOT, ".agent-queue", "failed"))
  ? readdirSync(join(ROOT, ".agent-queue", "failed")).filter((f) => f.endsWith(".md"))
  : [];
if (failedTasks.length > 0) {
  console.log(`${failedTasks.length} task(s) failed and are waiting in .agent-queue/failed/:`);
  for (const f of failedTasks) console.log(`  ${f}`);
  console.log(
    "\n  Why:     npm run auto:status\n" +
      "  Retry:   npm run queue -- retry        (bounded — twice, then it needs you)\n",
  );
}

// Reconciliation. done/ is read as proof a task's code is committed, and dependents are
// released on that basis, so a done task with no commit behind it corrupts the ordering of
// everything after it. That has happened twice here and was caught by hand both times.
const audit = spawnSync("node", [join(ROOT, "scripts", "agent-queue.mjs"), "audit"], {
  encoding: "utf8",
});
if (audit.status !== 0) {
  console.log((audit.stdout ?? "").trim());
  console.log("");
}

const finalAhead = branch === WORK_BRANCH ? aheadOf(`origin/${WORK_BRANCH}`) : "";

if (!finalAhead || finalAhead === "0") {
  console.log(`Nothing is waiting for you — ${WORK_BRANCH} has no unpushed commits.`);
} else {
  console.log(`${finalAhead} commit(s) are built, verified, and committed on ${WORK_BRANCH}.`);
  console.log("Nothing has been pushed; that is the only step left, and it is yours.\n");
  run("git", ["log", "--oneline", `origin/${WORK_BRANCH}..HEAD`]);
  console.log(`
  Review:  git diff origin/${WORK_BRANCH}..HEAD
  Push:    git push`);
}

console.log(`\nHistory: npm run auto:status`);
