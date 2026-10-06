#!/usr/bin/env node
/**
 * The work intake — what makes the loop start without a person typing a command.
 *
 *   npm run queue -- add "add a /v1/reports/:id/summary endpoint"
 *   npm run queue -- add --file ./specs/summary-endpoint.md
 *   npm run queue -- plan                          # turn new specs/*.md into queued tasks
 *   npm run queue                                  # list what is waiting
 *   npm run queue -- drain                         # work the queue down, one at a time
 *   npm run queue -- drain --max 5                 # stop after five tasks
 *   npm run queue -- resume                        # requeue whatever an interrupted run left behind
 *
 * A drain refuses to spend past AGENT_MAX_USD_PER_DRAIN, checked before each task begins and
 * never mid-task — a task killed halfway leaves a dirty tree and a half-finished change,
 * which is worse than letting it finish. Anything still in todo/ when the ceiling is reached
 * stays there, untouched: an untouched task has not failed, and the next scheduled run
 * starts with a fresh budget. Exit status 3 says the ceiling stopped it; 1 says work failed.
 *
 * A task is a markdown file. Anything that can write a file can therefore create work:
 * a person, a cron entry, a CI failure hook, another agent. `drain` is the piece meant to
 * run on a schedule — it takes whatever is waiting, does it, and records the outcome.
 *
 *   .agent-queue/todo/     waiting — tracked: the queue every checkout of the branch shares
 *   .agent-queue/doing/    in flight (a crashed run leaves its file here) — local, gitignored
 *   .agent-queue/done/     landed on this machine — local, gitignored
 *   .agent-queue/failed/   attempted, never verified — local; the failure is in the journal
 *
 * What another checkout knows comes from git, never from these local lanes: a planned spec is
 * a `Plan <spec>` commit adding its briefs to todo/ and its hash to planned.json, and a landed
 * task is a commit that removes its brief from todo/ and names it on a `Task:` line.
 *
 * Everything is built on the work branch, in this checkout. Tasks run strictly one at a time and
 * each commits to the work branch before the next begins — which is exactly what lets a task build
 * on the one before it, and what makes a `Depends-on:` chain advance on its own.
 *
 * This replaced a branch-per-task model that ran tasks in parallel worktrees. That model
 * isolated tasks from each other, but a dependent could not start until its dependency was
 * *merged*, so chains sat blocked behind the owner's merges. Serial-on-main trades the
 * parallelism for a queue that keeps moving.
 *
 * What it deliberately does NOT do: merge, or push unless asked. Push is off by default and
 * happens after each verified commit only with `AGENT_AUTO_PUSH=1`. Merging a PR stays forever human.
 */
import { spawn, spawnSync } from "node:child_process";
import { findCommitFor, isDefinite } from "./lib/commit-pairing.mjs";
import { landedTaskStems } from "./lib/landed-tasks.mjs";
import {
  SKIPPED_SPECS,
  failedPlanAttempts,
  isPlannableSpecPath,
  planNameProblems,
  specDigest,
  specSlug,
  specsChangedOn,
  strayPlanPaths,
} from "./lib/planning.mjs";
import { workingTreePaths } from "./lib/changed-paths.mjs";
import { planDirName, plannedEntry } from "./lib/build-record.mjs";
import { ADHOC, fsKey, listTasks, nameOf, qualify, scopeOf, stemOf } from "./lib/task-names.mjs";
import { wantsRefuseDirtyStart } from "./lib/agent-flags.mjs";
import { UNTRACKED_DIR, hasSalvage, restoreSalvage } from "./lib/salvage.mjs";
import { carriesProductCodeFromShow } from "./lib/landed.mjs";
import {
  existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync, renameSync, rmSync,
  rmdirSync, unlinkSync,
} from "node:fs";
import { join, dirname, resolve, basename, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { IS_WINDOWS, hasExecutable, isAlive, killTree, sleepSync, spawnPortable } from "./lib/proc.mjs";
import { HEARTBEAT_MS, heartbeat, release, tryAcquire } from "./lib/queue-lock.mjs";
import { dependencyState as depState } from "./lib/task-deps.mjs";
import {
  assertValidConfig, BASE_BRANCH, config, modelArgs, positiveInt, resolveModel, WORK_BRANCH, WORKFLOW,
  workflowRefusal,
} from "./lib/project-config.mjs";
import { buildPlanBrief } from "./lib/brief-plan.mjs";
import { renderStream } from "./lib/stream-render.mjs";
import { appendRun, clearTaskHistory, priorTaskRuns, readRuns } from "./run-journal.mjs";
import { accountBlockedUntil, minutesUntil } from "./lib/account-block.mjs";
import {
  describeSpend,
  emptySpend,
  journalCostFields,
  parseAssumedUsd,
  spendOfEntries,
  spendSummaryLines,
  sumSpend,
} from "./lib/spend.mjs";
import {
  BUDGET_EXIT_CODE,
  DEFAULT_MAX_USD_PER_DRAIN,
  ceilingReached,
  chargeAgainstCeiling,
  describeCeiling,
  formatCeiling,
  parseCeiling,
} from "./lib/budget.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const QUEUE = join(ROOT, ".agent-queue");
const LANES = ["todo", "doing", "done", "failed"];
// The port a task's live checks prefer. It is only a preference — verify.mjs moves off it
// when something it cannot tie to this checkout is already answering there.
const BASE_PORT = Number(config.verify.service?.port ?? 3000) + 1;
/**
 * agent-run.mjs exits 4 when the *account* cannot run — a usage limit, an expired key.
 * Module scope on purpose: it is read inside runTask's close handler, and a copy declared
 * further down the file is both invisible there and easy for a revert to wipe. That is
 * exactly what happened, and the drain crashed on ReferenceError mid-run.
 */
const EXIT_BLOCKED = 4;
/** The planner's model — the same rule as the runner's, so no session runs on an unchosen default. */
const PLAN_MODEL = resolveModel({ env: process.env.AGENT_MODEL });
// What one `claude` call is charged at when its stream carried no readable cost. Scripts
// read the environment directly; the setting is documented in .env.example.
const ASSUMED_USD = parseAssumedUsd(process.env.AGENT_ASSUMED_USD_PER_CALL);
// What one drain may spend in total, planning included. Checked before each task begins and
// never mid-task: killing a task halfway leaves a dirty tree and a half-finished change,
// which is worse than the marginal spend of letting it finish. `off` means no ceiling.
const MAX_USD_PER_DRAIN = parseCeiling(
  process.env.AGENT_MAX_USD_PER_DRAIN,
  DEFAULT_MAX_USD_PER_DRAIN,
);

assertValidConfig();

const argv = process.argv.slice(2);
const action = argv.find((a) => !a.startsWith("--")) ?? "list";
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

const laneDir = (lane) => join(QUEUE, lane);
const ensureLanes = () => {
  for (const lane of LANES) mkdirSync(laneDir(lane), { recursive: true });
};

/**
 * Only one process may move task files at a time.
 *
 * A task is claimed by renaming it out of todo/, which is not atomic across processes: two
 * drains list the same todo/ a moment apart, both try the rename, and the loser dies with
 * ENOENT part-way through — taking its live slot with it. So the whole mutating path
 * (intake, claim, and the lane moves at the end) runs under one lock.
 *
 * The rules — atomic stale takeover, a heartbeat so a reused pid cannot hold it, a lock
 * from another host judged by heartbeat alone — live in lib/queue-lock.mjs, where they are
 * tested. This is only the process wiring around them.
 */
const LOCK = join(QUEUE, "drain.lock");

/**
 * The runner child the drain is waiting on, if any. A signal that stops the drain must stop
 * it too: releasing the lock while it still ran let the next timer fire take the lock,
 * requeue doing/, and park or revert the tree under a session that was still writing to it.
 */
let activeChild = null;
let heldLock = null;

function acquireLock(what) {
  if (heldLock) return;
  mkdirSync(QUEUE, { recursive: true });
  for (let attempt = 0; attempt < 5; attempt++) {
    const res = tryAcquire(LOCK, { isAlive });
    if (res.ok) {
      heldLock = res.lock;
      if (res.tookOver) {
        console.log(`Took over a stale queue lock left by pid ${res.tookOver.pid} (no longer running).`);
      }
      const timer = setInterval(() => heartbeat(LOCK, heldLock), HEARTBEAT_MS);
      timer.unref();
      process.on("exit", () => release(LOCK, heldLock));
      for (const sig of ["SIGINT", "SIGTERM", "SIGHUP"]) {
        process.on(sig, () => {
          if (activeChild?.pid) {
            console.error(`\nStopping the running task (pid ${activeChild.pid}) before releasing the queue.`);
            killTree(activeChild.pid);
          }
          release(LOCK, heldLock);
          process.exit(130);
        });
      }
      return;
    }
    if (res.holder) {
      console.error(
        `Another ${what} is already running (pid ${res.holder.pid}${res.holder.host ? ` on ${res.holder.host}` : ""}).\n` +
          "Two at once race over the same task files, so this one is stopping instead.\n" +
          `Wait for it to finish, or if you are sure it is gone: rm ${LOCK.replace(ROOT, ".")}`,
      );
      process.exit(1);
    }
    // The lock changed hands mid-takeover; look again in a moment.
    sleepSync(200);
  }
  console.error("Could not take the queue lock. Try again.");
  process.exit(1);
}
// Task files live in their scope's folder inside each lane (lib/task-names.mjs), so a lane
// lists as `scope/name.md` and a move creates the folder it lands in and drops the one it
// emptied — an empty scope folder in todo/ would otherwise linger as noise.
const listLane = (lane) => listTasks(laneDir(lane));

function moveTask(file, from, to) {
  const target = join(laneDir(to), file);
  mkdirSync(dirname(target), { recursive: true });
  renameSync(join(laneDir(from), file), target);
  dropEmptyScope(from, file);
}

function dropEmptyScope(lane, file) {
  const scope = scopeOf(stemOf(file));
  if (!scope) return;
  try {
    rmdirSync(join(laneDir(lane), scope));
  } catch {
    /* not empty, or already gone */
  }
}

/**
 * A task may declare `Depends-on: 01-foo, 02-bar` in its first few lines. It stays in
 * todo/ until every named task has landed, which means "already committed to the work branch",
 * so a dependent genuinely sees the code it was waiting for. A bare name means the sibling in
 * the task's own scope; `<scope>/<name>` reaches another plan's task.
 */
function dependenciesOf(lane, file) {
  const head = readFileSync(join(laneDir(lane), file), "utf8").split("\n").slice(0, 12);
  const line = head.find((l) => /^depends-on\s*:/i.test(l.trim()));
  if (!line) return [];
  return line
    .split(":")
    .slice(1)
    .join(":")
    .split(",")
    .map((d) => d.trim().replace(/\.md$/, ""))
    .filter((d) => d && !/^none$/i.test(d))
    .map((d) => qualify(d, stemOf(file)));
}

/**
 * With every task committed to the work branch in place, "verified" and "available to the next
 * task" are the same event: agent-run.mjs commits before the task leaves doing/, so a task
 * sitting in done/ is a task whose code is already on the work branch for its dependents to build
 * against.
 *
 * That holds only because runTask() *enforces* it: a task reaches done/ when HEAD actually
 * moved, never merely because the runner exited 0. Reading done/ as proof of landed code
 * while filing tasks there on exit status alone is what let verified work go missing.
 *
 * This is what makes a chain self-advancing. The branch-per-task model this replaced also
 * required the dependency to be *merged*, because a dependent branched from the work branch and
 * could not see work still sitting uncommitted in another worktree — so a chain stopped
 * dead until the owner merged. Nothing here waits on a human any more.
 */
//
// Names match exactly (lib/task-deps.mjs). A name no lane holds is reported as `unknown`
// rather than waited on silently, since nothing will ever satisfy it.
//
// done/ is local to this machine, so it is not the whole answer any more: a dependency
// landed by a teammate, or on this branch before a fresh clone, is known only to history.
// Both count. The history read is cached on HEAD, which is what changes when a task lands.
let landedCache = { head: null, stems: new Set() };
function landedStems() {
  const head = (git(["rev-parse", "HEAD"]).stdout ?? "").trim();
  if (head !== landedCache.head) landedCache = { head, stems: head ? landedTaskStems(ROOT) : new Set() };
  return landedCache.stems;
}

function dependencyState(stem) {
  const state = depState(stem, {
    todo: listLane("todo"),
    doing: listLane("doing"),
    done: listLane("done"),
    failed: listLane("failed"),
  }, landedStems());
  return state === "landed" ? "landed" : state === "unknown" ? "unknown" : "unbuilt";
}

function blockedBy(file) {
  return dependenciesOf("todo", file)
    .map((d) => ({ dep: d, state: dependencyState(d) }))
    .filter((d) => d.state !== "landed");
}

const slugify = (text) =>
  text.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 40) || "task";

const git = (args, opts = {}) => spawnSync("git", args, { cwd: ROOT, encoding: "utf8", ...opts });


// --- intake: specs/ → queued tasks -------------------------------------------------
const SPECS = join(ROOT, "specs");
const PLANNED = join(QUEUE, "planned.json");
const digest = specDigest;
const loadPlanned = () => {
  try {
    return JSON.parse(readFileSync(PLANNED, "utf8"));
  } catch {
    return {};
  }
};

const currentBranch = () => (git(["rev-parse", "--abbrev-ref", "HEAD"]).stdout ?? "").trim();

/**
 * Move what a planning call wrote outside its folder into `dir`, losslessly, and put those
 * paths back as HEAD has them. Tracked edits become one patch; untracked files are moved
 * with their directories kept, so nothing has to be guessed back into place.
 */
function parkStrayPaths(paths, dir) {
  mkdirSync(dir, { recursive: true });
  const tracked = paths.filter((p) => git(["ls-files", "--error-unmatch", "--", p]).status === 0);
  const untracked = paths.filter((p) => !tracked.includes(p));
  if (tracked.length) {
    const patch = git(["diff", "--binary", "HEAD", "--", ...tracked], { maxBuffer: 64 * 1024 * 1024 }).stdout ?? "";
    if (patch.trim()) writeFileSync(join(dir, "tracked.patch"), patch);
    git(["restore", "--source=HEAD", "--staged", "--worktree", "--", ...tracked]);
  }
  for (const p of untracked) {
    const target = join(dir, "untracked", p);
    mkdirSync(dirname(target), { recursive: true });
    try {
      renameSync(join(ROOT, p), target);
    } catch {
      // Already gone; the clean-tree check after intake reports anything that remains.
    }
  }
}

/** Every task name already used in `scope` — queued in any lane, or landed in history. */
function takenNames(scope) {
  const names = new Set();
  for (const lane of LANES) {
    for (const f of listLane(lane)) if (scopeOf(stemOf(f)) === scope) names.add(nameOf(stemOf(f)));
  }
  for (const stem of landedStems()) if (scopeOf(stem) === scope) names.add(nameOf(stem));
  return [...names].sort();
}

/** Stop with the reason when the workflow forbids planning or building on this branch. */
function refuseByWorkflow() {
  const refusal = workflowRefusal({ branch: currentBranch() });
  if (!refusal) return;
  console.error(refusal);
  process.exit(1);
}

/**
 * Team workflow only: the specs this branch added or changed since it left the base branch,
 * committed or not — see lib/planning.mjs `specsChangedOn` for why only those.
 *
 * The base is whichever of the local and the remote-tracking base branch is further along,
 * so a stale local copy does not make specs merged since look like this branch's own.
 * Returns undefined, after saying so, when no merge base can be found: planning someone
 * else's spec is the mistake this exists to prevent, so the answer is then "none".
 */
function specsOfThisBranch() {
  const bases = [BASE_BRANCH, `origin/${BASE_BRANCH}`]
    .map((ref) => (git(["merge-base", ref, "HEAD"]).stdout ?? "").trim())
    .filter(Boolean);
  const mergeBase = bases.reduce(
    (newest, sha) => (!newest || git(["merge-base", "--is-ancestor", newest, sha]).status === 0 ? sha : newest),
    "",
  );
  if (!mergeBase) {
    console.log(
      `team workflow: no common history with ${BASE_BRANCH} was found, so no spec is planned.\n` +
        "Fetch the base branch, or set project.baseBranch in agentic.config.json.",
    );
    return undefined;
  }
  const committed = (git(["diff", "--name-only", mergeBase, "HEAD", "--", "specs"]).stdout ?? "").split("\n");
  const uncommitted = (git(["status", "--porcelain", "--untracked-files=all", "--", "specs"]).stdout ?? "")
    .split("\n")
    .filter(Boolean)
    .map((l) => l.slice(3).replace(/^.* -> /, ""));
  return specsChangedOn([...committed, ...uncommitted]);
}

/**
 * Commit what planning one spec produced — the spec itself, its briefs, and its line in
 * planned.json — and nothing else.
 *
 * Left uncommitted, all of it rode along in the first task's commit, and in a team it never
 * reached anyone until that task landed: a teammate's loop saw the spec as unplanned and
 * planned it again, differently. Committed at once, the plan travels with the branch.
 *
 * `git commit -- <paths>` commits exactly these paths whatever else is staged. A refused
 * commit — a hook, no git identity — is reported, never fatal: the briefs are queued
 * locally either way, and only sharing them waits.
 */
function commitPlan(specFile, added, replaced = []) {
  // A withdrawn brief that was ever committed leaves a deletion to commit with the plan
  // that replaced it; one that never was has nothing to record.
  const withdrawn = replaced
    .map((f) => `.agent-queue/todo/${f}`)
    .filter((p) => git(["ls-files", "--error-unmatch", "--", p]).status === 0);
  const paths = [
    `specs/${specFile}`,
    ...added.map((f) => `.agent-queue/todo/${f}`),
    ...withdrawn,
    ".agent-queue/planned.json",
  ];
  const body = [
    `Spec: specs/${specFile}`,
    ...(added.length ? ["", ...added.map((f) => `- ${f.replace(/\.md$/, "")}`)] : []),
    ...(replaced.length ? ["", "Replaces, unbuilt:", ...replaced.map((f) => `- ${f.replace(/\.md$/, "")}`)] : []),
    ...(config.project.coAuthor ? ["", config.project.coAuthor] : []),
  ].join("\n");
  const add = git(["add", "--", ...paths]);
  const commit = add.status === 0 ? git(["commit", "-m", `Plan ${specFile}`, "-m", body, "--", ...paths]) : add;
  if (commit.status !== 0) {
    console.error(
      `  ${specFile}: planned, but the plan could not be committed — commit ${paths.join(", ")} by hand ` +
        `so other checkouts see it.\n    ${(commit.stderr || commit.stdout || "").trim().split("\n")[0]}`,
    );
    return;
  }
  console.log(`  committed Plan ${specFile}`);
}

/**
 * Plan every spec that has not been planned at its current contents.
 *
 * Returns what the planning cost. Planning is a `claude` call like any other and is real
 * money; a drain total that counted only the tasks would understate itself by however many
 * specs arrived that cycle, which is exactly the spend nobody is watching.
 */
async function planSpecs() {
  if (!existsSync(SPECS)) return emptySpend();

  const planned = loadPlanned();
  const ours = WORKFLOW === "team" ? specsOfThisBranch() : null;
  if (ours === undefined) return emptySpend();
  const pending = readdirSync(SPECS)
    .filter((f) => f.endsWith(".md") && !SKIPPED_SPECS.has(f))
    .filter((f) => !ours || ours.has(f))
    .filter((f) => plannedEntry(planned[f]).hash !== digest(readFileSync(join(SPECS, f), "utf8")));

  if (pending.length === 0) return emptySpend();

  if (!hasExecutable("claude")) {
    console.log(`${pending.length} unplanned spec(s) in specs/, but the claude CLI is not on PATH.`);
    return emptySpend();
  }

  const costs = [];

  console.log(`Planning ${pending.length} new spec(s) from specs/…`);
  for (const file of pending) {
    // Intake is the one phase that can make an unbounded number of calls: twenty specs
    // arriving at once is twenty `claude` calls, and nothing between them would otherwise
    // notice the drain's ceiling. Checked per spec for that reason. A spec left unplanned
    // is not lost — planned.json is keyed on contents, so the next run picks it up.
    if (ceilingReached(sumSpend(costs, ASSUMED_USD).usd, MAX_USD_PER_DRAIN)) {
      console.log(
        `  stopping intake: ` +
          `${describeCeiling("AGENT_MAX_USD_PER_DRAIN", sumSpend(costs, ASSUMED_USD).usd, MAX_USD_PER_DRAIN)}. ` +
          `Leaving the remaining spec(s) unplanned for the next run.`,
      );
      break;
    }

    const body = readFileSync(join(SPECS, file), "utf8");
    const specHash = digest(body);
    const failedBefore = failedPlanAttempts(readRuns(), file, specHash);
    if (failedBefore >= config.budget.planAttempts) {
      console.error(
        `  ${file}: this version already failed to plan ${failedBefore} time(s) ` +
          `(budget.planAttempts = ${config.budget.planAttempts}), so it is not planned again. ` +
          "Edit the spec — any change earns it fresh attempts — or read why in `npm run auto:status`.",
      );
      continue;
    }
    const before = new Set(listLane("todo"));
    const dirtyBefore = workingTreePaths(ROOT);
    // The spec's scope: its task folder in every lane and the prefix of every `Task:` line
    // its commits carry. Planning the same spec again (after an edit) reuses it, so the
    // names it already used — queued in any lane, or landed in history — are handed to the
    // planner to number past, and refused if it reuses one: a new `01-x` would otherwise be
    // read as the old plan's landed `01-x`. Only the build-record directory is stamped with
    // when planning started.
    const startedAt = new Date().toISOString();
    const scope = specSlug(file);
    const taken = takenNames(scope);
    const builds = planDirName(scope, new Date(startedAt));
    // Planning an edited spec again: whatever of its old plan is still waiting in todo/ was
    // split from the version that no longer exists. Left queued, both plans were built —
    // the old tasks and the new ones covering the same ground. They are withdrawn to
    // .agent-runs/superseded/ for the new plan to replace, and put back if it fails, so a
    // failed re-plan loses nothing. Their names stay taken.
    const supersededDir = join(ROOT, ".agent-runs", "superseded", builds);
    const replaced = planned[file]
      ? [...before].filter((f) => scopeOf(stemOf(f)) === scope)
      : [];
    for (const f of replaced) {
      mkdirSync(dirname(join(supersededDir, f)), { recursive: true });
      renameSync(join(laneDir("todo"), f), join(supersededDir, f));
      before.delete(f);
    }
    const restoreReplaced = () => {
      for (const f of replaced) {
        mkdirSync(dirname(join(laneDir("todo"), f)), { recursive: true });
        renameSync(join(supersededDir, f), join(laneDir("todo"), f));
      }
      if (replaced.length) console.error(`  ${file}: the ${replaced.length} task(s) it would have replaced are back in todo/.`);
    };
    if (replaced.length) {
      console.log(`  ${file}: changed since it was planned; withdrawing ${replaced.length} unbuilt task(s) from its old plan.`);
    }
    // What this spec's folder already holds, so a planner that writes over a queued brief —
    // a name it was told not to reuse — is caught and the brief put back, not lost.
    const queued = new Map(
      [...before].filter((f) => scopeOf(stemOf(f)) === scope).map((f) => [f, readFileSync(join(laneDir("todo"), f), "utf8")]),
    );

    // stream-json so planning is watchable as it happens. With text the log sits unchanged
    // for the whole call, and an unattended planner that shows nothing looks broken.
    //
    // On the same model as the runner. The planner passed no `--model` at all and so ran on
    // whatever the machine's CLI default was — the one session in the loop nobody chose.
    // The spec goes in on stdin, like the runner's brief, so its length never meets
    // Windows' command-line limit; the CLI is started through proc.mjs so a `claude.cmd`
    // shim resolves too.
    //
    // Bounded like a task attempt: turns and dollars by the CLI itself, wall clock by the
    // timer below. The dollar cap is the smaller of the planner's own and what the drain
    // has left. No Bash: the planner reads and writes task files, and has nothing to run.
    const drainLeft = Number.isFinite(MAX_USD_PER_DRAIN)
      ? MAX_USD_PER_DRAIN - (sumSpend(costs, ASSUMED_USD).usd ?? 0)
      : Infinity;
    const planUsd = Math.max(0.01, Math.min(config.budget.planMaxUsd, drainLeft));
    const child = spawnPortable(
      "claude",
      [
        "-p",
        ...modelArgs(PLAN_MODEL),
        "--permission-mode", "bypassPermissions",
        "--disallowedTools", "Bash,PowerShell",
        "--max-turns", String(config.budget.planMaxTurns),
        "--max-budget-usd", planUsd.toFixed(2),
        "--output-format", "stream-json", "--verbose",
      ],
      { cwd: ROOT, stdio: ["pipe", "pipe", "inherit"] },
    );
    child.stdin.on("error", () => {});
    child.stdin.end(`${buildPlanBrief({ scope, taken, replaced: replaced.map((f) => nameOf(stemOf(f))) })}${body}`);
    const rendered = renderStream(child.stdout);
    let timedOut = false;
    const timer = setTimeout(() => {
      timedOut = true;
      console.error(`  ${file}: planning passed ${config.budget.planMaxMinutes} min (budget.planMaxMinutes); stopping it.`);
      killTree(child.pid);
    }, config.budget.planMaxMinutes * 60_000);
    const status = await new Promise((r) => {
      child.on("error", () => r(1));
      child.on("close", (code) => r(timedOut ? 1 : (code ?? 1)));
    });
    clearTimeout(timer);
    // Spread rather than cherry-pick — the same mistake that once left `subagents`
    // undefined at a call site would leave the planner's cost on the floor here.
    const res = { status, ...(await rendered) };

    // Recorded whether or not the planning call succeeded: a failed call still spent.
    // The journal is the one ledger, so `auto:status` and a drain's total both see this
    // without a second file to keep in step. `kind: "plan"` marks it as not an attempt at
    // any task, which is what stops a failed planning call being fed back to an unrelated
    // task as its own history.
    const cost = typeof res.costUsd === "number" ? res.costUsd : null;
    costs.push(cost);
    appendRun({
      id: `plan-${startedAt.replace(/[:.]/g, "-")}-${file}`,
      kind: "plan",
      task: `plan spec: ${file}`,
      branch: (git(["rev-parse", "--abbrev-ref", "HEAD"]).stdout ?? "").trim(),
      startedAt,
      finishedAt: new Date().toISOString(),
      attempts: 1,
      specHash,
      outcome: res.status === 0 ? "planned" : "failed",
      ...journalCostFields([cost], ASSUMED_USD),
    });
    // A refusal after the call is journalled as one more entry rather than by rewriting the
    // first: the journal is append-only, and failedPlanAttempts counts either.
    const journalRejected = (why) =>
      appendRun({
        id: `plan-${startedAt.replace(/[:.]/g, "-")}-${file}-rejected`,
        kind: "plan",
        task: `plan spec: ${file}`,
        branch: (git(["rev-parse", "--abbrev-ref", "HEAD"]).stdout ?? "").trim(),
        startedAt,
        finishedAt: new Date().toISOString(),
        attempts: 0,
        specHash,
        outcome: "rejected",
        failure: why,
      });

    // Whatever the call wrote outside the queue and specs/ is not a plan. Left in the tree
    // it would ride into the first task's commit, so it is moved aside — even from a call
    // that failed — and the plan is refused.
    const stray = strayPlanPaths(dirtyBefore, workingTreePaths(ROOT));
    if (stray.length > 0) {
      const aside = join(ROOT, ".agent-runs", "rejected-plans", builds, "stray");
      parkStrayPaths(stray, aside);
      console.error(
        `  ${file}: the planner changed ${stray.length} file(s) outside the queue, which it may not:\n` +
          stray.slice(0, 10).map((p) => `    ${p}`).join("\n") +
          `\n  Moved to ${relative(ROOT, aside).replace(/\\/g, "/")}/.`,
      );
    }

    if (res.status !== 0) {
      console.error(`  ${file}: planning failed; leaving it unplanned so the next run retries.`);
      restoreReplaced();
      continue;
    }

    const added = listLane("todo").filter((f) => !before.has(f));
    const problems = planNameProblems({ added, scope, taken });
    if (stray.length > 0) problems.push(`changed ${stray.length} file(s) outside the queue`);
    for (const [f, text] of queued) {
      const path = join(laneDir("todo"), f);
      const now = existsSync(path) ? readFileSync(path, "utf8") : null;
      if (now === text) continue;
      problems.push(`${f}: an already queued task was ${now === null ? "deleted" : "rewritten"} — put back`);
      writeFileSync(path, text);
    }
    if (problems.length > 0) {
      // Nothing the call wrote is queued: one misnamed brief can release a dependent early,
      // and a half-accepted batch is a split nobody planned. Moved aside, never deleted, and
      // planned.json is left alone so the next run plans the spec again.
      const aside = join(ROOT, ".agent-runs", "rejected-plans", builds);
      for (const f of added) {
        mkdirSync(dirname(join(aside, f)), { recursive: true });
        renameSync(join(laneDir("todo"), f), join(aside, f));
        dropEmptyScope("todo", f);
      }
      console.error(
        `  ${file}: the planner's task names were refused, so nothing from it was queued:\n` +
          problems.map((p) => `    ${p}`).join("\n") +
          `\n  Its files are in ${relative(ROOT, aside).replace(/\\/g, "/")}/; the next run plans the spec again.`,
      );
      journalRejected(problems.join("; "));
      restoreReplaced();
      continue;
    }

    console.log(`  ${file} → ${added.length} task(s) queued`);
    // Recorded against the contents, so editing a spec re-plans it and an untouched one
    // is never planned twice. `builds` is where this plan's tasks record (record-build.mjs):
    // a new directory per planning, so the records of each plan stay together.
    planned[file] = { hash: specHash, builds };
    writeFileSync(PLANNED, `${JSON.stringify(planned, null, 2)}\n`);
    commitPlan(file, added, replaced);
  }

  const total = sumSpend(costs, ASSUMED_USD);
  console.log(`Planning spend: ${describeSpend(total)}`);
  return total;
}

// --- add ----------------------------------------------------------------------------
if (action === "add") {
  ensureLanes();
  const file = argOf("file", null);
  const inline = argv.slice(argv.indexOf("add") + 1).find((a) => !a.startsWith("--"));
  const body = file ? readFileSync(file, "utf8") : inline;

  if (!body || !body.trim()) {
    console.error('Usage: npm run queue -- add "<task>"   |   npm run queue -- add --file <path>');
    process.exit(1);
  }

  const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
  // A task with no spec belongs to the adhoc scope; its intake stamp keeps the name unique.
  const name = `${ADHOC}/${stamp}-${slugify(body.split("\n")[0])}.md`;
  mkdirSync(join(laneDir("todo"), ADHOC), { recursive: true });
  writeFileSync(join(laneDir("todo"), name), body.trim().endsWith("\n") ? body : `${body}\n`);
  console.log(`Queued: .agent-queue/todo/${name}`);
  process.exit(0);
}

// --- list / status --------------------------------------------------------------------
if (action === "list" || action === "status") {
  ensureLanes();
  const counts = Object.fromEntries(LANES.map((l) => [l, listLane(l).length]));
  console.log(
    `todo ${counts.todo}  ·  doing ${counts.doing}  ·  done ${counts.done}  ·  failed ${counts.failed}\n`,
  );
  for (const lane of LANES) {
    const files = listLane(lane);
    if (files.length === 0) continue;
    console.log(`${lane}/`);
    for (const f of files.slice(0, 12)) {
      const first = readFileSync(join(laneDir(lane), f), "utf8").split("\n")[0].slice(0, 84);
      const listBase = spawnSync("git", ["rev-parse", "--abbrev-ref", "HEAD"], {
        cwd: ROOT,
        encoding: "utf8",
      }).stdout.trim();
      const blocked = lane === "todo" ? blockedBy(f, listBase) : [];
      const mark = blocked.length
        ? `  [blocked: ${blocked.map((b) => `${b.dep} (${b.state})`).join(", ")}]`
        : "";
      console.log(`  ${f}${mark}\n     ${first}`);
    }
    if (files.length > 12) console.log(`  … ${files.length - 12} more`);
    console.log("");
  }
  if (counts.doing > 0) {
    console.log("Files in doing/ are from an interrupted run. `npm run queue -- resume` requeues them.");
  }
  process.exit(0);
}

// --- clean ------------------------------------------------------------------------------
/**
 * The failed lane was a dead end: a task that exhausted its attempts landed there and
 * nothing ever looked at it again. Nothing retried it, nothing escalated it, and the loop
 * report did not even mention it — so a task could stop making progress and the only
 * evidence was a number in a status line nobody read.
 *
 * `retry` puts failed tasks back in todo/. It is deliberately bounded: a task that has
 * already been retried RETRY_LIMIT times is not requeued again, because a task failing the
 * same way for the third time is not going to pass on the fourth and every attempt costs
 * real money. At that point it needs a person to read the journal and change something.
 */
const RETRIES = join(QUEUE, "retries.json");
const RETRY_LIMIT = 2;

/**
 * How many times one brief may be *started* before the drain stops handing it back to
 * `todo/`.
 *
 * A run that exits BLOCKED is put back untouched, because the usual cause — a usage limit,
 * DNS, an expired key — is about the account and not about the task. That is right for the
 * cause it was written for and wrong for every cause that is really about this one brief:
 * the task returns to todo/, the timer fires, it is started again, and nothing in the loop
 * counts. One brief went round that circuit thirty times for $108.44.
 *
 * So the count is kept where it survives a restart — the journal — and after this many runs
 * the brief goes to `failed/` for a person to look at, which is exactly what "blocked, and
 * not by the account" means. `queue -- retry` puts it back when the blocker is cleared.
 */
// Validated: `abc` read as NaN, and `started >= NaN` is never true, so the limit vanished.
const RUN_LIMIT_PER_TASK = positiveInt(process.env.AGENT_MAX_RUNS_PER_TASK, 3, "AGENT_MAX_RUNS_PER_TASK");

const loadRetries = () => {
  try {
    return JSON.parse(readFileSync(RETRIES, "utf8"));
  } catch {
    return {};
  }
};

if (action === "retry") {
  acquireLock("retry");
  ensureLanes();
  const only = argv.find((a) => !a.startsWith("--") && a !== "retry");
  const failed = listLane("failed").filter((f) => !only || f.includes(only));

  if (failed.length === 0) {
    console.log(only ? `No failed task matching "${only}".` : "Nothing in failed/. Nothing to retry.");
    process.exit(0);
  }

  const retries = loadRetries();
  let moved = 0;
  const exhausted = [];

  for (const file of failed) {
    const count = retries[file] ?? 0;
    if (count >= RETRY_LIMIT) {
      exhausted.push({ file, count });
      continue;
    }
    retries[file] = count + 1;
    // Requeueing by hand is the one gesture that means somebody looked at why this stopped.
    // It is therefore also what clears the task's spend history, so a task held back by the
    // per-task ceiling gets a real budget again rather than being refused for ever.
    clearTaskHistory(stemOf(file));
    moveTask(file, "failed", "todo");
    console.log(`requeued  ${file}  (attempt ${count + 2}, spend history cleared)`);
    moved++;
  }

  writeFileSync(RETRIES, `${JSON.stringify(retries, null, 2)}\n`);
  console.log(`\n${moved} task(s) moved back to todo/.`);

  if (exhausted.length) {
    console.log(
      `\n${exhausted.length} task(s) have been retried ${RETRY_LIMIT} times and were NOT requeued:`,
    );
    for (const e of exhausted) console.log(`  ${e.file}`);
    console.log(
      "\nThese need a person. Read why they failed (`npm run auto:status`), then either fix\n" +
        "the brief, fix what blocks them, or delete them. Retrying unchanged only spends money.",
    );
  }
  process.exit(exhausted.length ? 1 : 0);
}

/**
 * Reconciliation: does what the queue claims match what the work branch actually holds?
 *
 * done/ is read as proof a task's code is committed, and dependents are released on that
 * basis — so a done task with no commit behind it silently corrupts the ordering of
 * everything after it. That has happened here twice, in different weeks, and was found by
 * hand both times. This is the check that would have found it in seconds.
 *
 * The evidence is the `Task: <stem>` line every runner commit carries, read from the history
 * reachable from HEAD. It survives a rebase and a squash merge alike, which a recorded commit
 * hash does not — so a branch squashed into its base still audits clean.
 *
 * Read-only by default. `--fix` repairs only mechanically recoverable strays; it never
 * moves a "says it landed, the work branch does not have it" brief.
 */
if (action === "audit") {
  ensureLanes();
  // `--fix` repairs what is mechanically recoverable instead of printing it for a person.
  // Reporting a correct problem and then waiting is still a stopped pipeline: one finished
  // task sitting in the wrong lane held up eight dependents until somebody noticed by hand.
  const FIX = process.argv.includes("--fix");
  // Repairs move lane files, which is exactly what a running drain does too.
  if (FIX) acquireLock("audit --fix");

  const commits = (git(["log", "--no-merges", "--format=%H%x09%s%x09%B%x00"], { maxBuffer: 256 * 1024 * 1024 }).stdout ?? "")
    .split("\0")
    .map((r) => r.replace(/^\n/, ""))
    .filter(Boolean)
    .map((entry) => {
      const [sha, subject, message = ""] = entry.split("\t");
      return { sha, subject, message };
    });
  // Merge commits are left out of the pairing list above, which is right for subjects; a
  // squash lands as an ordinary commit, so its `Task:` lines are still in it.
  const landed = landedStems();

  const done = listLane("done");
  // Claims in done/ with no commit reachable from HEAD naming them.
  const notOnBranch = [];
  const bookkeepingOnly = [];

  for (const file of done) {
    const stem = file.replace(/\.md$/, "");
    if (!landed.has(stem)) {
      notOnBranch.push(file);
      continue;
    }
    // A commit that carries only the brief's own lane move is not the work. Six tasks once
    // landed exactly that way — subjects claiming real features, one of them carrying any
    // application code — and both the queue and this audit called it done.
    const hit = findCommitFor({ taskFile: file, brief: "", commits });
    if (!hit) continue;
    const touched = git(["show", "--name-only", "--format=", hit.sha]).stdout;
    if (!carriesProductCodeFromShow(touched)) bookkeepingOnly.push({ file, sha: hit.sha });
  }

  // A task whose commit is already on the work branch but whose brief never left todo/ or
  // failed/ — work that landed some other way, or on another machine before this pull.
  const strays = [];
  for (const lane of ["todo", "failed"]) {
    for (const file of listLane(lane)) {
      const brief = readFileSync(join(laneDir(lane), file), "utf8");
      const hit = findCommitFor({ taskFile: file, brief, commits });
      if (hit) strays.push({ lane, file, ...hit });
    }
  }

  console.log(`Audited ${done.length} task(s) in done/ against the history of ${currentBranch()}.\n`);

  if (strays.length) {
    const repairable = strays.filter(isDefinite);
    console.log(`${strays.length} task(s) whose commit is on the work branch but which never reached done/:`);
    for (const st of strays) {
      console.log(
        `  ${st.lane}/${st.file}  →  ${st.sha.slice(0, 7)}  (${st.matchedBy})` +
          (isDefinite(st) ? "" : "  — too weak a match to act on"),
      );
    }

    if (!FIX) {
      console.log(
        `\nEach one is holding back every task that depends on it. Run\n` +
          `\`npm run queue -- audit --fix\` to file the ${repairable.length} definite match(es).`,
      );
    } else {
      console.log("");
      const removedFromTodo = [];
      for (const st of repairable) {
        moveTask(st.file, st.lane, "done");
        console.log(`  filed  ${st.file}  →  done/  against ${st.sha.slice(0, 7)}`);
        if (st.lane === "todo") removedFromTodo.push(`.agent-queue/todo/${st.file}`);
      }
      tidyTodo(removedFromTodo);
      const weak = strays.length - repairable.length;
      if (weak > 0) {
        console.log(`  left alone: ${weak} match(es) too weak to act on — check them by hand.`);
      }
    }
  }

  if (notOnBranch.length) {
    console.log(`\n${notOnBranch.length} task(s) say they landed, and the work branch does not have them:`);
    for (const f of notOnBranch) console.log(`  ${f}  (no commit reachable from HEAD names it on a Task: line)`);
    console.log(
      "\n`done/` released dependents against code that is not here. Usually the branch was switched\n" +
        "or reset after the task landed; otherwise requeue the work or delete the brief. Decide by\n" +
        "hand — `--fix` leaves every brief in this class exactly where it is.",
    );
  }
  if (bookkeepingOnly.length) {
    console.log(`\n${bookkeepingOnly.length} task(s) whose commit carries only queue bookkeeping:`);
    for (const b of bookkeepingOnly) console.log(`  ${b.file}  →  ${b.sha.slice(0, 8)}`);
    console.log(
      "\nThe brief moved, but no application file changed.\n" +
        "The feature these describe does not exist. Requeue them.",
    );
  }
  if (!strays.length && !notOnBranch.length && !bookkeepingOnly.length) {
    console.log("Every finished task names a commit on this branch that carries it.");
  }
  process.exit(bookkeepingOnly.length || notOnBranch.length ? 1 : 0);
}

/**
 * Commit the removal of briefs that `audit --fix` filed as landed. todo/ is shared, so a
 * brief left there would stay "waiting" in every other checkout; the commit is what tells
 * them. Skipped where the workflow forbids committing — the base branch of a team — with
 * the paths named, so the removal can travel with the next pull request instead.
 */
function tidyTodo(paths) {
  const tracked = paths.filter((p) => git(["ls-files", "--error-unmatch", "--", p]).status === 0);
  if (tracked.length === 0) return;
  if (workflowRefusal({ branch: currentBranch() })) {
    console.log(`  not committed on ${currentBranch()} (team workflow): ${tracked.join(", ")}`);
    return;
  }
  const stage = git(["rm", "-q", "--cached", "--", ...tracked]);
  const commit = stage.status === 0
    ? git(["commit", "-m", "Tidy the queue: remove briefs that already landed", "--", ...tracked])
    : stage;
  console.log(
    commit.status === 0
      ? `  committed the removal of ${tracked.length} landed brief(s) from todo/`
      : `  could not commit the removal of ${tracked.join(", ")}: ${(commit.stderr || "").trim().split("\n")[0]}`,
  );
}

// --- resume ------------------------------------------------------------------------
// Stopping a drain (Ctrl+C, a closed laptop, a killed process) leaves the tasks that were
// in flight sitting in doing/. This puts them back in the queue.
if (action === "resume") {
  // Under the same lock as drain: requeueing what is in doing/ while a drain is live would
  // hand a second run the task the first one is still working on.
  acquireLock("drain");
  ensureLanes();
  const stranded = listLane("doing");
  if (stranded.length === 0) {
    console.log("Nothing was interrupted — doing/ is empty.");
  } else {
    parkStranded(stranded);
    for (const file of stranded) {
      moveTask(file, "doing", "todo");
      console.log(`requeued  ${file}`);
    }
    console.log(
      `\n${stranded.length} task(s) back in the queue. What an interrupted task left in the tree ` +
        "is parked under .agent-runs/interrupted/ and restored when it runs again, so its next " +
        "run verifies that work before rebuilding any of it.",
    );
  }
  spawnSync("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"], { stdio: "inherit" });
  process.exit(0);
}

if (action === "plan") {
  // planSpecs() writes new task files into todo/; a drain listing todo/ at the same moment
  // would see a half-written intake.
  refuseByWorkflow();
  acquireLock("drain");
  ensureLanes();
  // Awaited: planSpecs streams its planner output now, so an unawaited call would print the
  // queue listing before a single task file had been written.
  await planSpecs();
  spawnSync("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"], { stdio: "inherit" });
  process.exit(0);
}

if (action !== "drain") {
  console.error(`Unknown action "${action}". Use: add | list | status | plan | drain | resume | retry | audit`);
  process.exit(1);
}

// --- drain --------------------------------------------------------------------------------
ensureLanes();
// One working tree cannot hold two tasks at once. Refusing is deliberate: a run that
// accepted --parallel 3 and silently built one task at a time would misreport its own
// behaviour, and one that truly interleaved three would have them overwrite each other.
if (argOf("parallel", null)) {
  console.error(
    "--parallel is no longer supported: tasks are built on the work branch in this checkout, one at\n" +
      "a time. Two tasks sharing one working tree would overwrite each other's changes.",
  );
  process.exit(1);
}
const max = Number(argOf("max", "0")) || Infinity;
// `budget.attempts` from the config (three — loop.mjs states the evidence), passed through so
// the drain and a direct `npm run auto` agree.
const attempts = argOf("attempts", String(config.budget.attempts));
/**
 * Work happens on the work branch, in place. There is no base to choose and no branch to cut: each
 * task is built in this checkout, verified, and committed to the work branch before the next one
 * starts. That commit is precisely what lets the next task see the previous task's code.
 *
 * Both conditions below are requirements, not conveniences. Building on some other branch
 * would put the work where the next task cannot see it, and starting dirty would sweep
 * somebody else's uncommitted changes into this task's commit.
 */
/**
 * Move everything uncommitted aside, losslessly, and leave the tree clean.
 *
 * Tracked edits — staged and unstaged — are written out as a patch *before* the revert that
 * follows them, because the revert is otherwise where they die. That is the loss the
 * 2026-08-28 incident was actually about: a failed run's finished work sat in the tree, the
 * next drain refused to start rather than clearing it, and the queue froze for hours.
 * Untracked files are moved rather than copied, so the tree ends genuinely clean.
 *
 * Nothing is ever deleted. `.agent-queue/` is excluded throughout: reverting it makes a
 * claimed task reappear as runnable and the drain spins on it forever, which has happened.
 *
 * Returns the park directory, or null when the tree was already clean — in which case no
 * directory is created and nothing is printed, so a clean run behaves exactly as before.
 */
function parkWorkingTree(slug) {
  const dirty = ((spawnSync("git", ["status", "--porcelain", "--untracked-files=all"], {
    cwd: ROOT, encoding: "utf8",
  }).stdout ?? "")
    .split("\n")
    .filter((l) => l.trim() && !l.slice(3).trim().startsWith(".agent-queue/") && !isPlannableSpecPath(l)));
  if (dirty.length === 0) return null;

  const parked = join(ROOT, ".agent-runs", "interrupted", slug);
  // A park still holding work — one whose restore failed — is moved aside rather than
  // written over: its patch may be the only copy of that work.
  if (hasSalvage(parked)) renameSync(parked, `${parked}-older-${new Date().toISOString().replace(/[:.]/g, "-")}`);
  mkdirSync(parked, { recursive: true });

  // A spec is the person's request, not a run's leftovers, so it stays where they saved it.
  const keep = [":(exclude).agent-queue", ":(exclude,glob)specs/*.md"];

  // HEAD-relative and including staged changes, so one patch restores the whole tracked
  // state with `git apply`. Written first: everything after this line destroys it.
  // --binary, or a changed image or font is written as "Binary files differ" and lost.
  const patch = spawnSync("git", ["diff", "--binary", "HEAD", "--", ".", ...keep], {
    cwd: ROOT, encoding: "utf8", maxBuffer: 64 * 1024 * 1024,
  }).stdout ?? "";
  if (patch.trim()) writeFileSync(join(parked, "tracked.patch"), patch);

  const untracked = dirty
    .filter((l) => l.startsWith("??"))
    .map((l) => l.slice(3).trim())
    .filter(Boolean);

  git(["reset", "--", ".", ...keep]);
  git(["checkout", "--", ".", ...keep]);

  for (const pathname of untracked) {
    // Directories kept (lib/salvage.mjs UNTRACKED_DIR), so restoring needs no guessing.
    const target = join(parked, UNTRACKED_DIR, pathname);
    try {
      mkdirSync(dirname(target), { recursive: true });
      renameSync(join(ROOT, pathname), target);
    } catch {
      // A directory, or already gone. The clean-tree check below reports what remains
      // rather than this silently pretending it was handled.
    }
  }

  return { dir: parked, patched: Boolean(patch.trim()), untracked: untracked.length };
}

/**
 * Park the tree a dead run left behind under that task's own key, so its next run restores
 * it as salvage. Only the dead run can have written it — the drain lock was held — but the
 * clean-tree check that follows refused it, so a crash or a reboot froze the queue until a
 * person cleared the tree by hand. With more than one task stranded the tree cannot be
 * attributed, and the clean-tree check decides as before.
 */
function parkStranded(stranded) {
  if (stranded.length !== 1) return;
  const parked = parkWorkingTree(fsKey(stemOf(stranded[0])));
  if (parked) {
    console.log(`  its leftovers are parked in ${relative(ROOT, parked.dir).replace(/\\/g, "/")}/ for its next run`);
  }
}

function assertCleanMain() {
  const branch = currentBranch();
  refuseByWorkflow();
  if (branch !== WORK_BRANCH) {
    console.error(
      `The queue builds on ${WORK_BRANCH}, and this checkout is on "${branch}".\n` +
        `Switch to ${WORK_BRANCH} first:  git checkout ${WORK_BRANCH}`,
    );
    process.exit(1);
  }

  // Queue bookkeeping under .agent-queue/ does not count: this runs after the drain lock
  // is taken, and a task claimed by an interrupted earlier run may already have moved.
  // Nor does a spec waiting to be planned: saving one is how work starts, and intake
  // commits it with its plan. assertNoUnplannedSpecs() checks again once intake has run.
  const dirty = ((spawnSync("git", ["status", "--porcelain"], {
    cwd: ROOT, encoding: "utf8",
  }).stdout ?? "")
    .split("\n")
    .filter((l) => l.trim() && !l.slice(3).startsWith(".agent-queue/") && !isPlannableSpecPath(l)));
  if (dirty.length > 0) {
    // When a human (or Cursor) shares this checkout, parking silently feels like data loss.
    // Default (fully agentic): refuse. Set AGENT_REFUSE_DIRTY_START=0 to park under interrupted/.
    if (wantsRefuseDirtyStart()) {
      console.error(
        `${dirty.length} uncommitted file(s) block this drain (AGENT_REFUSE_DIRTY_START):\n\n` +
          dirty.slice(0, 15).join("\n") +
          (dirty.length > 15 ? `\n  …and ${dirty.length - 15} more` : "") +
          "\n\nCommit, stash, or clear them, then re-run. " +
          "Set AGENT_REFUSE_DIRTY_START=0 to park WIP under .agent-runs/interrupted/ instead.",
      );
      process.exit(1);
    }

    // Refusing here is what froze the queue: the drain stopped, said so only in
    // .agent-runs/drain.log, and nobody was awake to read it. The tree still has to be
    // clean — a task's commit must hold only its own work — but clearing it is something
    // this can do itself, as long as nothing is discarded.
    console.log(
      `${dirty.length} uncommitted file(s) were in the way. Moving them aside rather than stopping.`,
    );
    const parked = parkWorkingTree(`drain-${new Date().toISOString().replace(/[:.]/g, "-")}`);
    const still = ((spawnSync("git", ["status", "--porcelain"], {
      cwd: ROOT, encoding: "utf8",
    }).stdout ?? "")
      .split("\n")
      .filter((l) => l.trim() && !l.slice(3).trim().startsWith(".agent-queue/") && !isPlannableSpecPath(l)));

    if (still.length > 0) {
      console.error(
        "The working tree is still dirty after moving what could be moved:\n\n" +
          still.slice(0, 10).join("\n") +
          "\n\nCommit or stash these, then re-run.",
      );
      process.exit(1);
    }

    const rel = parked ? relative(ROOT, parked.dir).replace(/\\/g, "/") : "";
    console.log(
      `Parked in ${rel}/ — nothing was deleted.` +
        (parked?.patched ? `\n  Restore tracked edits:  git apply ${rel}/tracked.patch` : "") +
        (parked?.untracked ? `\n  ${parked.untracked} untracked file(s) moved there as-is.` : ""),
    );
  }
}

// Before the lock, before anything is claimed: if the account said when it recovers, wait
// for it. Checked here rather than per task because the refusal is about the account, so
// every task would meet it identically — and on a one-minute timer that is sixty pointless
// runs an hour, each one journalled against whichever brief happened to be first.
const blockedUntil = accountBlockedUntil();
if (blockedUntil) {
  console.log(
    `The account is refusing until ${blockedUntil.toLocaleTimeString()} ` +
      `(${minutesUntil(blockedUntil)} min). Nothing claimed, nothing spent; the next drain after that runs.`,
  );
  process.exit(0);
}

refuseByWorkflow();
acquireLock("drain");

/**
 * Without the CLI there is no run to have. Checked once, here, rather than left to each
 * task: agent-run.mjs exits on the same condition, so a drain that skipped this check would
 * march the whole queue through the failed lane one task at a time, reporting each as a
 * failure of the work rather than of the environment. Nothing was wrong with those tasks.
 */
if (!hasExecutable("claude")) {
  console.error(
    "The `claude` CLI is not on PATH, so no task can be built.\n" +
      "Nothing has been moved between lanes — the queue is exactly as you left it.\n\n" +
      "Install Claude Code, or check the PATH this process inherited. When run from a\n" +
      "scheduled task that is the PATH the scheduler starts it with, not your terminal's.",
  );
  process.exit(1);
}

/**
 * Anything left in doing/ is from a run that died — a crash, a closed laptop, a killed
 * process — because the lock is held now and a live drain could not have released it while
 * still owning a task. Requeue it rather than leaving it stranded.
 *
 * This is what `resume` does by hand. Doing it automatically is the difference between a
 * timer that recovers from a reboot on its own and one that quietly stops making progress
 * until somebody notices a file sitting in a lane and knows the command.
 *
 * The task is requeued, never resumed mid-flight. A dead run's partial edits are not
 * trusted as they stand, but they are not thrown away either: parkStranded() moves them
 * under the task's key, and its next run restores them and verifies before rebuilding.
 */
const stranded = listLane("doing");
if (stranded.length > 0) {
  console.log(`Requeuing ${stranded.length} task(s) left behind by a run that did not finish:`);
  parkStranded(stranded);
  for (const file of stranded) {
    moveTask(file, "doing", "todo");
    console.log(`  ${file}`);
  }
  console.log("");
}

assertCleanMain();

// Intake: turn anything new in specs/ into queued tasks before deciding what to run, so
// dropping a file into specs/ is the whole act of starting work.
//
// Planning is a `claude` call and is real money, so the drain ceiling covers it too. It is
// checked here, before intake, rather than only before the first task: a drain with nothing
// left to spend must not pay to split a spec it then cannot build. Nothing has been spent
// yet at this point, so this only ever refuses a ceiling of zero — which is precisely what
// makes "spend nothing this cycle" mean nothing at all.
const drainCeilingSpent = ceilingReached(0, MAX_USD_PER_DRAIN);
if (drainCeilingSpent) {
  console.log(
    `Not planning: ${describeCeiling("AGENT_MAX_USD_PER_DRAIN", 0, MAX_USD_PER_DRAIN)}, ` +
      "so this cycle may not spend on intake either.",
  );
}
const planSpend =
  flag("no-plan") || drainCeilingSpent ? emptySpend() : await planSpecs();

// Intake commits every spec it plans, so a spec still uncommitted here was not planned —
// it failed, it was over the ceiling, or (in a team) it is not this branch's. Building now
// would sweep it into the first task's commit as if that task had written it, so the drain
// stops and names it instead.
const unplannedSpecs = ((spawnSync("git", ["status", "--porcelain", "--untracked-files=all"], {
  cwd: ROOT, encoding: "utf8",
}).stdout ?? "")
  .split("\n")
  .filter((l) => l.trim() && isPlannableSpecPath(l)));
if (unplannedSpecs.length > 0) {
  console.error(
    `\n${unplannedSpecs.length} spec(s) are uncommitted and were not planned this run:\n\n` +
      unplannedSpecs.join("\n") +
      "\n\nNothing is built while they are, so no task's commit carries them. Read the planning " +
      "output above, then fix the spec and run again, or move it out of specs/ until it is " +
      "ready (`npm run queue -- plan` plans specs without building).",
  );
  process.exit(1);
}

/**
 * Split todo/ into what can run now and what is still waiting on a dependency.
 *
 * Called again after every task, never once up front. A task's Depends-on is satisfied the
 * moment the task it names lands on the work branch, and that happens *during* a drain — so a set
 * computed before the first task would hold a whole dependency chain back to one task per
 * scheduled run. An eight-task chain would take eight hours of wall clock to do what one
 * pass can do continuously.
 */
function partitionTodo() {
  const ready = [];
  const blocked = [];
  for (const file of listLane("todo")) {
    const waitingOn = blockedBy(file);
    (waitingOn.length ? blocked : ready).push({ file, waitingOn });
  }
  return { ready, blocked };
}

const firstPass = partitionTodo();
for (const b of firstPass.blocked) {
  const detail = b.waitingOn
    .map((w) => `${w.dep} (${w.state === "unknown" ? "no task by that name in any lane — check the Depends-on line" : "not built yet"})`)
    .join(", ");
  console.log(`holding  ${b.file} — waits on ${detail}`);
}

if (firstPass.ready.length === 0) {
  console.log(
    firstPass.blocked.length
      ? `\nNothing is runnable yet: ${firstPass.blocked.length} task(s) waiting on work that has not landed.`
      : "Queue is empty. Nothing to do.",
  );
  // A cycle that refused intake on cost did not end because there was nothing to do — there
  // may be specs sitting unplanned. Exiting 0 here would report it as an empty queue, which
  // is precisely the confusion the ceiling's exit status exists to prevent.
  if (drainCeilingSpent) {
    console.log(
      `\nIntake was refused on cost: ` +
        `${describeCeiling("AGENT_MAX_USD_PER_DRAIN", 0, MAX_USD_PER_DRAIN)}. ` +
        "Any unplanned spec is still waiting for the next run.",
    );
    process.exit(BUDGET_EXIT_CODE);
  }
  process.exit(0);
}

const totalQueued = firstPass.ready.length + firstPass.blocked.length;
console.log(
  `Building on ${WORK_BRANCH}, one at a time. ${firstPass.ready.length} runnable now` +
    (firstPass.blocked.length
      ? `, ${firstPass.blocked.length} held — each is re-checked as soon as a task lands.\n`
      : ".\n"),
);

/**
 * Build one task in this checkout and let agent-run.mjs commit it to the work branch.
 *
 * The commit is the whole point. It is what moves the task's code onto the work branch where the
 * next task — and any dependent still waiting in todo/ — can actually see it, and it is
 * what keeps tasks separate: the next one starts from a clean tree because this one ended
 * by committing everything it touched.
 *
 * A task that fails verification leaves its edits uncommitted, so they are reverted before
 * the next task starts. Otherwise a failure's half-finished work would be swept into the
 * next task's commit and attributed to it. Untracked leftovers are listed rather than
 * deleted — discarding work nobody has looked at is not this script's call.
 */
function runTask(file, index) {
  return new Promise((resolvePromise) => {
    // The park is keyed on the whole file stem. It was keyed on the subject cut to 40
    // characters with the stamp removed, so two sibling tasks with a long shared opening
    // shared one park — and the second was handed the first one's patch as "prior work, do
    // not start over". agent-run.mjs keys its own fallback restore on the stem too, which
    // is what makes that fallback find anything at all. A park written under the old key
    // is still restored, once, so work parked before this change is not stranded.
    // The stem (`scope/name`) is the task's identity; `slug` is it as one path segment, the
    // key its parked leftovers are filed under.
    const stem = stemOf(file);
    const slug = fsKey(stem);
    const legacySlug = slugify(slug.replace(/^[\d-T]+-/, ""));
    const taskPath = join(laneDir("doing"), file);
    moveTask(file, "todo", "doing");

    const before = git(["rev-parse", "HEAD"]).stdout.trim();
    // Where the journal ends before this task starts. What the task spent belongs to the
    // agent-run.mjs child process, and the journal is the channel back: the child appends
    // its entry before exiting, so anything past this mark when it closes is this task's.
    // Reading it beats parsing the child's stdout, which is a rendered narration and not a
    // record of anything.
    const journalMark = readRuns().length;

    const args = [
      join(ROOT, "scripts", "agent-run.mjs"),
      "--task-file", taskPath,
      "--attempts", attempts,
      "--verify-port", String(BASE_PORT),
    ];

    console.log(`\n[${index + 1}/${totalQueued}] ${slug}`);

    // Restore parked leftovers from a prior failed/blocked attempt *before* the runner
    // starts, so it sees a dirty tree of this task's work rather than a blank tree.
    const parkKey = hasSalvage(join(ROOT, ".agent-runs", "interrupted", slug))
      ? slug
      : hasSalvage(join(ROOT, ".agent-runs", "interrupted", legacySlug))
        ? legacySlug
        : null;
    if (parkKey) {
      const restored = restoreSalvage(ROOT, parkKey);
      if (restored) {
        console.log(
          `   Salvage restored from prior attempt` +
            (restored.patched ? " (patch applied)" : "") +
            (restored.untracked ? `, ${restored.untracked} untracked file(s)` : "") +
            (restored.error ? `\n   warning: ${restored.error}` : "") +
            " — runner will verify before rebuilding.",
        );
      }
    }

    // Detached on POSIX so the runner leads its own process group and a signal to the drain
    // can end the whole tree (proc.mjs's killTree sends to the group); Windows reaches the
    // tree with taskkill /T either way.
    // AGENT_FROM_DRAIN tells the runner the tree was clean when this drain started, so
    // anything in it now is this task's restored salvage, not someone's own edits.
    const child = spawn("node", args, {
      cwd: ROOT,
      stdio: "inherit",
      detached: !IS_WINDOWS,
      env: { ...process.env, AGENT_FROM_DRAIN: "1" },
    });
    activeChild = child;

    child.on("close", (code) => {
      activeChild = null;
      // 4 means the account itself cannot run — a usage limit, an expired key. Every
      // remaining task would fail identically, so the drain stops rather than emptying the
      // queue into failed/ for a reason no task caused.
      // 4 means the run never happened — a missing CLI, a usage limit, DNS, an attempt that
      // reached no turns and spent nothing. The task is untouched, so it goes back to todo/
      // rather than into the lane a person visits when work is broken. Everything queued
      // behind it would fare identically, so the drain stops rather than emptying the queue.
      if (code === EXIT_BLOCKED) {
        // Session / usage limits often hit *after* hours of writing. Park under the task
        // slug so the next drain can restore and salvage instead of rebuilding from zero.
        // (The ordinary !landed path below is skipped on this early return.)
        const parked = parkWorkingTree(slug);
        if (parked) {
          const rel = relative(ROOT, parked.dir).replace(/\\/g, "/");
          console.error(
            `\n[${index + 1}/${totalQueued}] ${slug}: BLOCKED mid-work — leftovers parked in ${rel}/` +
              (parked.patched ? " (tracked.patch ready to restore)" : "") +
              "\nPut back in todo/. Next drain restores this park and salvages; stopping so the limit can clear.",
          );
        } else {
          console.error(
            `\n[${index + 1}/${totalQueued}] ${slug}: NOT ATTEMPTED — nothing could run.\n` +
              "Put back in todo/ untouched. Stopping the drain; the next one starts fresh.",
          );
        }
        // Back to todo/ only while the journal says this brief has not already had its
        // runs. Past that it is not the account that is blocked, it is this task.
        // `.attempted`, not `.runs`: a run the account refused outright never tried this
        // task, and holding it against the brief is how a session limit emptied three of
        // them into failed/ overnight.
        const started = priorTaskRuns(readFileSync(taskPath, "utf8").trim(), undefined, stem).attempted;
        const lane = started >= RUN_LIMIT_PER_TASK ? "failed" : "todo";
        if (lane === "failed") {
          console.error(
            `${slug}: started ${started} time(s) already (AGENT_MAX_RUNS_PER_TASK=${RUN_LIMIT_PER_TASK}) — ` +
              "moved to failed/ instead of todo/. Clear the blocker, then `npm run queue -- retry`.",
          );
        }
        moveTask(file, "doing", lane);
        // A run refused before its first call journals nothing and spent nothing; only one
        // that reached the model (a limit hit mid-work) has a spend to report.
        const entries = readRuns().slice(journalMark);
        const spend = entries.length ? spendOfEntries(entries, ASSUMED_USD) : null;
        resolvePromise({ file, slug, ok: false, blocked: true, committed: false, commit: null, spend });
        return;
      }
      const ok = code === 0;
      // agent-run.mjs exits on its own status when a task stopped on AGENT_MAX_USD_PER_TASK
      // rather than on a failing gate. "Ran out of money" and "could not make the tests
      // pass" call for opposite responses from whoever reads the report, so the two are
      // carried apart from here all the way to `auto:status`.
      const stoppedOnBudget = code === BUDGET_EXIT_CODE;
      const after = git(["rev-parse", "HEAD"]).stdout.trim();
      const committed = ok && after !== before;

      // What this task spent, from whatever it journalled — including the case where it
      // journalled nothing at all, which spendOfEntries charges as one unmeasured call
      // rather than as free.
      const spend = spendOfEntries(readRuns().slice(journalMark), ASSUMED_USD);

      // done/ means "on the work branch", not "the runner exited 0". dependencyState() reads done/ as
      // proof a task's code is committed and releases its dependents on that basis, so a
      // task that verified but produced no commit must not be filed there — the next task
      // would build against a work branch that lacks it, which is exactly how work went missing
      // under the old worktree model. Verified-but-uncommitted is a failure for the queue.
      // "Committed" is not enough. Moving the brief from todo/ to done/ and writing a build
      // record are both commits, and a run that did no product work at all still moves HEAD
      // — which is exactly what happened: six tasks, six commits with subjects claiming real
      // features, and one of them carrying any application code. The dashboard was unchanged
      // and the queue said it was done.
      //
      // So a task lands only when its commit touches something outside the bookkeeping.
      const producedCode =
        committed &&
        carriesProductCodeFromShow(git(["show", "--name-only", "--format=", after]).stdout);

      if (ok && committed && !producedCode) {
        console.error(
          `\n[${index + 1}/${totalQueued}] ${slug}: its commit carries only queue bookkeeping.\n` +
            "No application file changed, so nothing was actually built.",
        );
      }

      const landed = ok && committed && producedCode;

      // Anything that did not land leaves the tree clean for the next task. A dirty tree is
      // not a neutral state here: the next task refuses to start on one, so leaving it dirty
      // stops the whole drain.
      //
      // The revert MUST exclude .agent-queue/. todo/ is tracked, so a plain
      // `git checkout -- .` restores the very task file this run just moved out of todo/ —
      // the task reappears as runnable, is picked again, fails again, and the drain spins on
      // one task forever. That happened here for thousands of iterations.
      //
      // It also reverts every *other* uncommitted file in the tree, which is how this bug
      // erased its own fix: an edit to this very function was wiped by a drain iteration
      // before it could be committed.
      //
      // Untracked files are moved aside rather than deleted. Discarding work nobody has
      // looked at is not this script's call, but leaving it in place blocks every task after
      // this one — so it is preserved where a person can find it, and the tree is clean.
      // Tracked edits are saved as a patch before the revert, not merely reverted. A run
      // that failed on turns rather than on correctness can be holding finished work, and
      // this used to destroy it — $22.82 of verified, reviewable change, in one case.
      if (!landed) {
        const parked = parkWorkingTree(slug);
        if (parked) {
          const rel = relative(ROOT, parked.dir).replace(/\\/g, "/");
          console.log(
            `   Leftovers parked in ${rel}/ so the next task starts clean — nothing deleted.` +
              (parked.patched ? `\n     Restore tracked edits:  git apply ${rel}/tracked.patch` : "") +
              (parked.untracked ? `\n     ${parked.untracked} untracked file(s) moved there as-is.` : ""),
          );
        }
      }

      // A local move only: doing/, done/ and failed/ are gitignored, so filing the brief
      // changes nothing git can see. The task's own commit already removed it from todo/ and
      // carries its build record (agent-run.mjs writes it before committing) — nothing is
      // left behind for the next task's commit to sweep up.
      moveTask(file, "doing", landed ? "done" : "failed");
      // Why it did not land, in the words the report will use. A budget stop is not a
      // verification failure and must not be reported as one: the task may be perfectly
      // sound and merely larger than its ceiling.
      const reason = landed
        ? null
        : stoppedOnBudget
          ? "stopped on the per-task spend ceiling (AGENT_MAX_USD_PER_TASK) — not a verification failure"
          : ok
            ? "verified, but nothing was committed to the work branch"
            : "verification never passed";
      const verdict = landed
        ? "committed"
        : stoppedOnBudget
          ? "STOPPED — per-task spend ceiling reached"
          : ok
            ? "FAILED — verified, but nothing was committed to the work branch"
            : "FAILED";
      console.log(`[${index + 1}/${totalQueued}] ${slug}: ${verdict} · ${describeSpend(spend)}`);
      resolvePromise({
        file,
        slug,
        ok: landed,
        committed,
        commit: committed ? after : null,
        spend,
        reason,
        stoppedOnBudget,
      });
    });
  });
}

// Strictly serial: one working tree, one task at a time, each committing before the next
// begins so the next starts clean and can build on what came before.
//
// The set is recomputed every iteration rather than iterated from a fixed list, so a task
// unblocked by the one that just landed runs immediately instead of waiting for the next
// scheduled fire. This is the difference between a dependency chain draining in one
// continuous pass and it advancing one task per hour.
//
// Termination is on the ready set going empty, not on a counter: runTask() always renames
// its file out of todo/ into done/ or failed/, so todo/ strictly shrinks and a task can
// never be picked twice. What remains blocked when nothing is ready is genuinely waiting on
// work that did not land, and is reported rather than retried.
const results = [];
// What this drain has spent so far, planning included. Planning is a `claude` call like any
// other and is real money, so a drain whose whole budget went on splitting an over-large
// spec must not then start building from it.
let drainSpentUsd = planSpend.usd ?? 0;
// Set when the ceiling stopped the drain, which is a different ending from an empty queue
// and must not be reported as one.
let budgetStop = null;
// Set when a run could not happen at all (exit 4). That stops the drain too, and is a
// third ending: neither an empty queue nor a spent budget.
let blockedStop = null;

if (Number.isFinite(MAX_USD_PER_DRAIN)) {
  console.log(`Spend ceiling for this drain: ${formatCeiling(MAX_USD_PER_DRAIN)} (AGENT_MAX_USD_PER_DRAIN)\n`);
}

while (results.length < max) {
  const { ready } = partitionTodo();
  if (ready.length === 0) break;

  // Checked here — before the task is claimed — and never again until it finishes. A task
  // killed halfway leaves a dirty tree and a half-finished change, which is worse than the
  // marginal spend of letting it run to its end, and the next task would refuse to start
  // on the mess. So the ceiling stops the *next* task, never the one in flight.
  //
  // Nothing is renamed on this path. An untouched task has not failed and must stay in
  // todo/ exactly as it was, for the next scheduled run — which starts with a fresh
  // budget — to pick up.
  if (ceilingReached(drainSpentUsd, MAX_USD_PER_DRAIN)) {
    // Everything in todo/, not only what was runnable: a held task is still waiting there
    // and still untouched, and saying "2 remain" when 5 files sit in the lane would read as
    // if the ceiling had somehow consumed the rest.
    budgetStop = { spentUsd: drainSpentUsd, waiting: listLane("todo").length };
    break;
  }

  const result = await runTask(ready[0].file, results.length);
  results.push(result);
  // A blocked run (exit 4) put its task back at the head of todo/, so carrying on would
  // claim the same task again at once — and every service or account refusal behind it
  // would meet the same wall. The loop used to do exactly that, spinning on one brief
  // until the drain ceiling's assumed per-run charge ran out. Nothing is charged: a run
  // that never happened spent nothing a ceiling should count.
  if (result.blocked) {
    blockedStop = result;
    break;
  }
  // The task's own total is already charged — spendOfEntries has replaced any unreadable
  // per-call figure with the assumed one — so it goes in as a measured number here. What
  // this call adds is the ceiling test, on the same helper the runner uses per attempt.
  const charged = chargeAgainstCeiling({
    spentUsd: drainSpentUsd,
    // A total that recorded nothing goes in as unknown, not as $0.00 — the same rule the
    // ledger opens with, applied at the one place a task's whole spend could otherwise
    // enter the drain's total as free. No current writer produces that shape; the day one
    // does, this must not be the line that makes it cost nothing.
    costUsd: result.spend?.recorded === false ? null : result.spend?.usd,
    ceilingUsd: MAX_USD_PER_DRAIN,
    assumedUsd: ASSUMED_USD,
  });
  drainSpentUsd = charged.spentUsd;
}

// Re-read rather than reusing the loop's last partition: when the loop stops on --max the
// final task's landing may have unblocked something, and reporting it as held would be wrong.
const held = partitionTodo().blocked;
if (held.length) {
  console.log(`\n${held.length} task(s) still held — their dependencies did not land:`);
  for (const b of held) {
    console.log(`  ${b.file} — waits on ${b.waitingOn.map((w) => w.dep).join(", ")}`);
  }
}

// --- report ---------------------------------------------------------------------------------
const ok = results.filter((r) => r.ok);
const failed = results.filter((r) => !r.ok && !r.blocked);
const budgeted = failed.filter((r) => r.stoppedOnBudget);

console.log(`\n${"=".repeat(70)}`);
console.log(
  `${ok.length} built and committed, ${failed.length - budgeted.length} failed` +
    `${budgeted.length ? `, ${budgeted.length} stopped on the per-task spend ceiling` : ""}` +
    `${blockedStop ? ", 1 could not run" : ""}.`,
);
console.log("=".repeat(70));

for (const r of ok) {
  console.log(`\n✓ ${r.slug}${r.commit ? `  ${r.commit.slice(0, 8)}` : ""}`);
}
for (const r of failed) {
  console.log(`\n${r.stoppedOnBudget ? "○" : "✗"} ${r.slug} — ${r.reason ?? "verification never passed"}`);
}

// What the run cost, next to what it built. A failed task spent money too, so it is listed
// here as well — the ledger records what was spent, not what was achieved. Nothing is
// refused on these numbers; they exist so that a ceiling, when there is one, has something
// real to be set from.
console.log("");
for (const line of spendSummaryLines(
  results.filter((r) => r.spend).map((r) => ({ label: r.slug, ok: r.ok, spend: r.spend })),
  planSpend,
)) {
  console.log(line);
}

// A drain that stopped on its ceiling ended for a different reason from one that ran out of
// work, and reporting the two the same way is how a budget stop on the timer would read as
// an empty queue. Everything still in todo/ is untouched — an untouched task has not failed
// — and the next scheduled run starts with a fresh budget and picks it up.
if (budgetStop) {
  console.log(
    `\nStopped on the drain spend ceiling: ` +
      `${describeCeiling("AGENT_MAX_USD_PER_DRAIN", budgetStop.spentUsd, MAX_USD_PER_DRAIN)}.`,
  );
  console.log(
    `${budgetStop.waiting} task(s) are still in todo/, untouched and unclaimed. Nothing was ` +
      "moved between lanes.\nThe next scheduled run starts a fresh budget and picks them up; " +
      "raise AGENT_MAX_USD_PER_DRAIN to let one\nrun go further.",
  );
}

if (ok.length > 0) {
  const ahead = (git(["rev-list", "--count", `origin/${WORK_BRANCH}..HEAD`]).stdout ?? "").trim();
  console.log(`
Everything is committed on ${WORK_BRANCH}.${ahead && ahead !== "0" ? ` It is ${ahead} commit(s) ahead of origin.` : ""}

  Review:  git log --oneline origin/${WORK_BRANCH}..HEAD
  Push:    git push          (yours alone — the guard refuses it from in here)`);
}

console.log(`\nHistory: npm run auto:status`);
if (blockedStop) {
  console.log(
    `\nStopped: ${blockedStop.slug} could not run (a service or the account refused before any ` +
      "work).\nIt is back in todo/; the next drain picks it up once the cause above is cleared.",
  );
}

// The exit status is the channel `loop.mjs` and the timer read: 3 means the ceiling stopped
// this drain, 4 means a run could not happen, 1 means work failed, 0 means the queue was
// worked down. A budget stop takes precedence — it is the reason there is more to do, and
// the failures are listed above.
process.exit(
  budgetStop ? BUDGET_EXIT_CODE : failed.length > 0 ? 1 : blockedStop ? EXIT_BLOCKED : 0,
);
