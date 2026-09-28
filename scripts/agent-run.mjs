#!/usr/bin/env node
/**
 * Unattended task runner — the autonomous loop.
 *
 *   npm run auto -- "add a GET /products/{id}/summary endpoint"
 *   npm run auto -- --task-file ./task.md
 *   npm run auto -- "fix the empty role list for tenant admins" --attempts 4
 *   npm run auto -- "..." --no-commit        # verify only, leave the work uncommitted
 *   npm run auto -- "..." --no-autostart     # never start the stack for a live check
 *   npm run auto -- "..." --verify-port 5100 # live checks against a dedicated API port
 *
 * Work happens on the work branch (`project.branch`, or whichever is checked out), in this
 * checkout. There is no task branch: the run starts from a clean tree, builds, verifies,
 * and commits there. That commit is what lets the next task in the queue build on this one.
 *
 * What it does, with no human in the loop:
 *   1. reads the journal for prior failed attempts at this task
 *   2. checks it is on a clean work branch — a dirty tree would fold someone else's work in
 *   3. if a prior attempt left parked work, restores it and verifies first — Claude then
 *      only reviews or fixes, rather than rebuilding from zero (salvage-on-retry)
 *   4. runs Claude Code headless on the task (or salvage brief)
 *   5. runs the verify script — the static gate, plus the live checks the diff demands
 *   6. on failure, feeds the actual output back and retries, up to --attempts — and no
 *      further than AGENT_MAX_USD_PER_TASK, which stops the retrying rather than warning
 *      about it and exits 3 so the caller can tell "out of money" from "never verified"
 *   7. records the run in .agent-runs/journal.jsonl either way
 *   8. commits to the work branch. Nothing is pushed unless AGENT_AUTO_PUSH=1 is set.
 *      Merging stays forever out of scope.
 *
 * Verification is scope-aware on purpose. The gate proves the code builds and its tests
 * pass; it says nothing about whether the running API still answers. verify.mjs looks at
 * what changed and refuses to pass an API change it could not exercise against a live stack.
 *
 * Permissions vs. guard rails: this runs Claude with permission prompts bypassed, because
 * a prompt with nobody to answer it is a hang. The .claude/hooks guards still run — they
 * are independent of permission mode — so reading secrets, dropping the database,
 * DROP/TRUNCATE/DELETE, force-push, pushing a protected branch, `git add -A`, and merging
 * anything stay blocked even here. Committing on the work branch is deliberately allowed —
 * that is how work accumulates for review. Pass --safe to require approval for edits.
 */
import { spawn, spawnSync } from "node:child_process";
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname, basename } from "node:path";
import { fileURLToPath } from "node:url";
import { appendRun, priorFailureBrief, priorTaskRuns, LOG_DIR } from "./run-journal.mjs";
import { formatForBrief, readLessons, selectLessons } from "./lib/memory.mjs";
import { renderStream } from "./lib/stream-render.mjs";
import { describeSpend, formatUsd, journalCostFields, parseAssumedUsd, sumSpend } from "./lib/spend.mjs";
import {
  BUDGET_EXIT_CODE,
  DEFAULT_MAX_USD_PER_TASK,
  chargeAgainstCeiling,
  describeCeiling,
  formatCeiling,
  parseCeiling,
} from "./lib/budget.mjs";
import { wantsAutoPush } from "./lib/agent-flags.mjs";
import { recordAccountBlock } from "./lib/account-block.mjs";
import { carriesProductCode } from "./lib/landed.mjs";
import {
  dirtyOutsideQueueLines,
  restoreSalvage,
  salvageBrief,
} from "./lib/salvage.mjs";
import { hasExecutable } from "./lib/proc.mjs";
import { config, WORK_BRANCH } from "./lib/project-config.mjs";
import { buildBrief } from "./lib/brief.mjs";
import {
  expectedSequence,
  explainMissing,
  explainMissingSkills,
  requiredAgents,
  sequenceProblems,
} from "./lib/departments.mjs";

// This runner is invoked with cwd set to a task worktree, which holds only *committed*
// code. Its own tooling must therefore be resolved relative to this file, not to cwd —
// otherwise a worktree branched from a commit that predates verify.mjs cannot verify at
// all. The rule: the runner drives with its own scripts, and verifies the worktree's code.
const SCRIPTS = dirname(fileURLToPath(import.meta.url));
// Resolved from this file like SCRIPTS above, for the same reason: cwd is the task's
// checkout, and the runner must find its own tooling regardless of where it is invoked.
const MEMORY_DIR = join(SCRIPTS, "..", ".claude", "memory");

const argv = process.argv.slice(2);
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

const taskFile = argOf("task-file", null);
// Line endings normalised: a brief checked out with CRLF would otherwise carry a `\r` into
// the commit subject and every line the prompt quotes.
const task = (taskFile ? readFileSync(taskFile, "utf8") : argv.find((a) => !a.startsWith("--")))?.replace(/\r\n/g, "\n");
// The queue's file name, which is what this task is called for as long as it exists. It is
// journalled so a later run can find this one's cost even after the brief's body is edited
// — matching on the body meant a brief that gained a `Depends-on:` line lost its own
// history, and with it the ceiling that history was holding up.
const taskSlug = taskFile ? basename(taskFile).replace(/\.md$/i, "") : null;

if (!task || !task.trim()) {
  console.error('Usage: npm run auto -- "<task>"  [--attempts N] [--safe] [--no-commit] [--no-autostart]');
  process.exit(1);
}

/**
 * The model the task's own session runs on.
 *
 * This was the single largest uncontrolled cost in the loop. Every subagent pins its model
 * in `.claude/agents/*.md`, but the session that spawns them pinned nothing, so it ran on
 * whatever the machine's default happened to be — and it is the session that does the most
 * work by far: it reads the codebase, writes the code, runs the commands, and drives all
 * eight agents. Its turns dwarf theirs.
 *
 * Opus by default, on the evidence. It was set to sonnet to save tokens and that was wrong in
 * both directions — measured across 23 landed tasks:
 *
 *   opus      2.3 attempts   24 min median   $12.74 per task
 *   sonnet    2.4 attempts   67 min median   $19.33 per task
 *
 * The attempt count barely moved; each attempt simply took three times as long, because a
 * weaker session spends more turns reaching the same place — and turns are both the clock and
 * the bill. There is no trade-off to weigh here: opus is faster and cheaper for this work.
 *
 * Re-measure before changing it back. `npm run auto:status` has the numbers, and the journal
 * now records turns per attempt, which is what made this answerable at all.
 *
 *   AGENT_MODEL=sonnet    slower and dearer here; measure before assuming otherwise
 *   AGENT_MODEL=inherit   pass no flag at all and take the CLI's own default
 */
const MODEL = (process.env.AGENT_MODEL ?? argOf("model", config.budget.model)).trim();

/**
 * How many times one task may be attempted before it is filed as failed.
 *
 * Two, on the evidence. Of 27 build runs, 11 went to a third attempt: they cost $211 —
 * nearly a third of all build spend — and 4 of them landed. Seven paid three times over and
 * produced nothing.
 *
 * This is the rule `queue -- retry` already applies at the other end, where RETRY_LIMIT is
 * 2 because "a task failing the same way a third time will not pass on the fourth, and every
 * attempt costs real money". A task that has failed twice needs its brief changed or its
 * blocker cleared, not a third identical run.
 *
 * Nothing is lost by stopping earlier: the task goes to `failed/`, where `queue -- retry`
 * puts it back — with a person having looked at why, which is the step that actually
 * changes the outcome. Raise it per run with `--attempts 3` when you have reason to.
 */
const MAX_ATTEMPTS = Number(argOf("attempts", config.budget.attempts));
/**
 * Turns one attempt may take before it is stopped — the *parent's* turns only, since every
 * subagent streams through the same feed and counting theirs punished the thorough runs.
 *
 * Set high on purpose. The real guard is the dollar ceiling, which is measured rather than
 * inferred and is what anyone actually cares about; this exists solely to catch a run that
 * has stopped converging, and the one it was written for reached 984. Every task it has
 * stopped since was inside its money budget — one at 401 turns had spent $17 of $50 — so a
 * tighter number was costing good work to prevent nothing.
 */
/** Exit code meaning "the account cannot run", distinct from a task that failed. */
const EXIT_BLOCKED = 4;
const MAX_TURNS = Number(
  process.env.AGENT_MAX_TURNS_PER_ATTEMPT ?? argOf("max-turns", String(config.budget.maxTurns)),
);
/** The port a live check prefers; it moves off it when a stranger is already there. */
const DEFAULT_VERIFY_PORT = String(config.verify.service?.port ?? 3000);
const PERMISSION_MODE = flag("safe") ? "acceptEdits" : "bypassPermissions";
const RUN_ID = `run-${new Date().toISOString().replace(/[:.]/g, "-")}`;
const startedAt = new Date().toISOString();

// What one `claude` call is charged at when its stream carried no readable cost. Scripts
// read the environment directly; the setting is documented in .env.example.
const ASSUMED_USD = parseAssumedUsd(process.env.AGENT_ASSUMED_USD_PER_CALL);

// One entry per attempt, in order, holding what that attempt's call reported — or null
// when it reported nothing. Kept for the journal rather than summed as we go: the
// per-attempt figures are what later tell a task that cost $8 in one pass from one that
// cost $8 across four, and a total alone cannot be told apart from a total that was
// half guessed.
const attemptCosts = [];
/** Parent turns per attempt. Zero across all of them means nothing ever reached the model. */
const attemptTurns = [];
/** Per attempt: how long it ran, and when each specialist was dispatched within it. */
const attemptPhases = [];

// What this task may spend in total, across every attempt. Read from the environment like
// the assumed figure above and documented in .env.example; `off` means no ceiling.
//
// The ceiling bounds *retrying*, not the attempt in flight: an attempt that has already
// been paid for runs to its end and is accepted if it verifies. Killing it halfway would
// leave a dirty tree and a half-finished change, and would have spent the money anyway.
const MAX_USD_PER_TASK = parseCeiling(
  process.env.AGENT_MAX_USD_PER_TASK,
  DEFAULT_MAX_USD_PER_TASK,
);
// Running total for this task, charged through lib/budget.mjs so that an attempt whose
// cost could not be read counts at the assumed figure rather than as free. A ceiling that
// unreadable costs slip past is no ceiling at all for exactly the runs most likely to need
// one.
let taskSpentUsd = 0;
let budgetExhausted = false;

const run = (cmd, args, opts = {}) =>
  spawnSync(cmd, args, { encoding: "utf8", stdio: "pipe", ...opts });
const runLive = (cmd, args) => spawnSync(cmd, args, { stdio: "inherit", encoding: "utf8" });

/**
 * Spawn a streaming `claude` call and narrate it while it runs.
 *
 * Async, unlike everything else here, because a synchronous spawn cannot show anything
 * until it returns — which is the whole problem this replaces. stderr stays inherited so
 * real errors land in the log untouched.
 */
async function runStreaming(cmd, args) {
  const child = spawn(cmd, args, { stdio: ["ignore", "pipe", "inherit"] });
  // The dollar ceilings in lib/budget.mjs are checked between tasks, deliberately: killing
  // a task mid-edit leaves a half-finished tree. But that leaves a single attempt able to
  // spend without limit, and one did — 984 turns and $240 against a $50 per-task ceiling.
  // Turns are observable while the run is still going, so they are the guard that can act.
  const rendered = renderStream(child.stdout, undefined, {
    maxTurns: MAX_TURNS,
    onLimit: () => child.kill("SIGTERM"),
  });
  const status = await new Promise((res) => {
    child.on("error", () => res(1));
    child.on("close", (code) => res(code ?? 1));
  });
  // Spread rather than cherry-pick. Naming the fields here meant that adding `subagents`
  // to renderStream left them undefined at the call site: a task passed verify, then the
  // enforcement that reads them crashed on it. A field added at one end must not need
  // remembering at the other.
  return { status, ...(await rendered) };
}
const branchName = () => run("git", ["rev-parse", "--abbrev-ref", "HEAD"]).stdout.trim();

function log(section) {
  console.log(`\n${"=".repeat(70)}\n${section}\n${"=".repeat(70)}`);
}

/** Record the run before exiting, so the journal never misses an outcome. */
function finish(outcome, { attempts, failure }) {
  appendRun({
    id: RUN_ID,
    task: task.trim(),
    taskSlug: taskSlug ?? undefined,
    branch: branchName(),
    startedAt,
    finishedAt: new Date().toISOString(),
    attempts,
    outcome,
    failure: failure ? failure.slice(-1200) : undefined,
    // What this run cost, per attempt and in total. The journal is one JSON line per run
    // and this rides on that same line — a second file would be a second thing to keep in
    // step. It is also the only channel back to the parent: a drain runs this script as a
    // child process, and reads the entry rather than parsing the child's stdout.
    ...journalCostFields(attemptCosts, ASSUMED_USD),
    // Parent-session turns per attempt. Journalled because the question "is the turn cap
    // too high?" came up and could not be answered: the cost of every run was on record and
    // the turns of none of them were, so the only honest answer was to leave the cap alone.
    // One number per attempt, so the next such question has data instead of a guess.
    turns: attemptTurns.slice(),
    // What each attempt's time was spent on. Turns say how much happened; this says when.
    phases: attemptPhases.slice(),
  });
}

// --- preflight ------------------------------------------------------------------
if (!hasExecutable("claude")) {
  console.error("The `claude` CLI is not on PATH. Install Claude Code first.");
  process.exit(1);
}

if (!existsSync(LOG_DIR)) mkdirSync(LOG_DIR, { recursive: true });

/**
 * Uncommitted changes that are *not* queue bookkeeping.
 *
 * `.agent-queue/todo/` is tracked, and claiming a task moves its file out of it — so by the
 * time the runner starts, the tree already shows that deletion. Counting it as "dirty"
 * would make the runner refuse every task the queue ever hands it. The move is part of the
 * task's own commit, which is where it belongs.
 */
function dirtyOutsideQueue(porcelain) {
  return dirtyOutsideQueueLines(porcelain);
}

function runVerifyCapture() {
  const verifyArgs = [
    join(SCRIPTS, "verify.mjs"),
    "--port",
    argOf("verify-port", DEFAULT_VERIFY_PORT),
    "--base",
    argOf("verify-base", WORK_BRANCH),
  ];
  if (!flag("no-autostart")) verifyArgs.push("--autostart");
  const verify = run("node", verifyArgs, { stdio: "pipe" });
  const output = `${verify.stdout ?? ""}\n${verify.stderr ?? ""}`;
  return { status: verify.status ?? 1, output };
}

// Work happens on the work branch, in place — no task branch, no worktree.
{
  const current = branchName();
  if (current !== WORK_BRANCH) {
    console.error(
      `This runner builds on ${WORK_BRANCH}, and the checkout is on "${current}".\n` +
        `Switch first:  git checkout ${WORK_BRANCH}`,
    );
    process.exit(1);
  }

  // Drain restores interrupted/<slug>/ before spawning us; if it did not (manual auto),
  // restore here from the task-file stem so salvage still works.
  const taskStem = taskFile ? basename(taskFile, ".md") : null;
  const repoRoot = join(SCRIPTS, "..");
  if (taskStem) {
    const restored = restoreSalvage(repoRoot, taskStem);
    if (restored) {
      log(
        `Restored salvage for ${taskStem}` +
          (restored.patched ? " (patch)" : "") +
          (restored.untracked ? `, ${restored.untracked} untracked` : "") +
          (restored.error ? ` — apply warning: ${restored.error}` : ""),
      );
    }
  }

  const dirty = dirtyOutsideQueue(run("git", ["status", "--porcelain"]).stdout);
  if (dirty.length > 0) {
    // Salvage path: prior attempt (or restored park) left WIP. Do not refuse — verify first
    // inside the attempt loop and tell Claude to review/fix rather than rebuild.
    log(
      `Salvage: ${dirty.length} uncommitted file(s) already in the tree — will verify before rebuilding`,
    );
  }
}

// --- the loop -----------------------------------------------------------------------
const history = priorFailureBrief(task);
if (history) log("This task has failed before — feeding the history into the brief");

// Cross-run memory. `history` above is this task's own past attempts; this is what *other*
// tasks learned. Without it every run rediscovers the same traps, because the journal is
// keyed by task and a lesson from task 5 is invisible to task 20.
const memory = (() => {
  try {
    const lessons = selectLessons(readLessons(MEMORY_DIR), task);
    const text = formatForBrief(lessons);
    if (text) log(`Carrying ${lessons.length} lesson(s) from earlier runs into the brief`);
    return text ? `\n${text}\n` : "";
  } catch (err) {
    // Memory improves a run; it is never the reason one cannot start.
    log(`Could not read memory (${err.message}) — continuing without it`);
    return "";
  }
})();

const BRIEF = buildBrief({ memory, history });

let feedback = "";
// Guidance that belongs to *how* the last attempt ended rather than to what verification
// said. Kept apart from `feedback` so both reach the retry, in the right order.
let turnLimitNote = "";
let stoppedAtLimitThisAttempt = false;
let passed = false;
let attempt = 0;

console.log(`Model: ${MODEL === "inherit" ? "the CLI default (AGENT_MODEL=inherit)" : MODEL} (AGENT_MODEL)`);
if (Number.isFinite(MAX_USD_PER_TASK)) {
  console.log(`Spend ceiling for this task: ${formatCeiling(MAX_USD_PER_TASK)} (AGENT_MAX_USD_PER_TASK)`);
}

// What this task cost in earlier runs counts against the ceiling too. Without this the
// ceiling bounded a *process*, not a task: every restart began at zero, so a task the drain
// kept requeueing could spend the ceiling again and again and never cross it once.
const carried = priorTaskRuns(task, ASSUMED_USD, taskSlug);
if (carried.runs > 0) {
  taskSpentUsd = carried.spend.usd;
  console.log(
    `Carried forward: ${carried.runs} earlier run(s) of this task spent ${formatUsd(taskSpentUsd)}.`,
  );
}
// Refused before the first attempt rather than after it. An attempt already in flight is
// allowed to finish because stopping it would have spent the money anyway; an attempt not
// yet started has cost nothing, and starting it is the decision this ceiling exists to make.
if (Number.isFinite(MAX_USD_PER_TASK) && taskSpentUsd >= MAX_USD_PER_TASK) {
  const reason =
    `Refused before starting: ` +
    `${describeCeiling("AGENT_MAX_USD_PER_TASK", taskSpentUsd, MAX_USD_PER_TASK)} ` +
    `across ${carried.runs} earlier run(s) of this same task. No attempt was made and nothing was spent.`;
  console.error(`\n${reason}`);
  console.error(
    "This task needs its brief changed or its blocker cleared, not another run. " +
      "Raise AGENT_MAX_USD_PER_TASK to override.",
  );
  finish("budget", { attempts: 0, failure: reason });
  process.exit(BUDGET_EXIT_CODE);
}

// `budgetExhausted` is checked here rather than mid-attempt: an attempt that has crossed
// the ceiling still finishes and is still accepted if it verifies, and what the ceiling
// stops is the attempt that would have come after it.
while (attempt < MAX_ATTEMPTS && !passed && !budgetExhausted) {
  attempt++;
  turnLimitNote = "";
  stoppedAtLimitThisAttempt = false;

  // Salvage-on-retry: if the tree already has WIP (restored park or prior attempt in this
  // run), verify it *before* spending another full Claude build. Reviews-only when green;
  // fix-only when red — never "implement the TASK from scratch" again.
  const dirtyNow = dirtyOutsideQueue(run("git", ["status", "--porcelain"]).stdout);
  let salvageMode = null; // null | "reviews-only" | "fix"
  let salvageVerifyOutput = "";
  if (dirtyNow.length > 0) {
    log(
      `Attempt ${attempt} of ${MAX_ATTEMPTS} — salvaging ${dirtyNow.length} existing file(s) (verify first)`,
    );
    const pre = runVerifyCapture();
    salvageVerifyOutput = pre.output;
    process.stdout.write(pre.output);
    if (pre.status === 0) {
      salvageMode = "reviews-only";
      log("Prior work verifies — Claude will only run department reviews, not rebuild");
    } else {
      salvageMode = "fix";
      log("Prior work does not verify — Claude will fix failures, not start from scratch");
    }
  } else {
    log(`Attempt ${attempt} of ${MAX_ATTEMPTS} — running Claude Code headless`);
  }

  const salvagePrefix =
    salvageMode === "reviews-only"
      ? salvageBrief({ verifies: true, verifyOutput: "" })
      : salvageMode === "fix"
        ? salvageBrief({ verifies: false, verifyOutput: salvageVerifyOutput })
        : "";

  const carried = [turnLimitNote, feedback].filter(Boolean).join("\n\n");
  // BRIEF already ends with `TASK:\n` — insert salvage *before* that marker so Claude sees
  // the salvage rules as part of the standing instructions, then the task text.
  const briefHead = BRIEF.replace(/\nTASK:\s*$/, "\n");
  const prompt = salvagePrefix
    ? `${briefHead}${salvagePrefix}\nTASK:\n${task}`
    : attempt === 1
      ? `${BRIEF}${task}`
      : `${BRIEF}${task}

The previous attempt did not verify. This is the actual output — fix the cause, not the
symptom, and do not work around it by loosening types, weakening a guard, or deleting the
check. Prefer editing the existing diff over rewriting from scratch:

${carried.slice(-6000)}`;

  // stream-json rather than text: text buffers the whole response and writes nothing until
  // the call returns, so a healthy run can sit silent for many minutes and read as a hang —
  // which is exactly when somebody kills it. The final event also carries total_cost_usd,
  // which is what a spend ceiling will need.
  const claudeRes = await runStreaming("claude", [
    "-p",
    prompt,
    ...(MODEL && MODEL !== "inherit" ? ["--model", MODEL] : []),
    "--permission-mode",
    PERMISSION_MODE,
    "--output-format",
    "stream-json",
    "--verbose",
  ]);

  attemptCosts.push(typeof claudeRes.costUsd === "number" ? claudeRes.costUsd : null);
  attemptTurns.push(typeof claudeRes.turns === "number" ? claudeRes.turns : 0);
  // Where this attempt's wall clock went: each delegation paired with the second it was
  // dispatched, and how long the attempt ran in total. One line per attempt, so the question
  // "which phase costs the 37 minutes" has an answer next time somebody asks instead of an
  // argument. Reviewers dispatched together share a timestamp — that is the point, it is how
  // a parallel tier tells itself apart from a sequential one.
  attemptPhases.push({
    elapsedSec: typeof claudeRes.elapsedSec === "number" ? claudeRes.elapsedSec : null,
    delegations: (claudeRes.subagents ?? []).map((name, i) => ({
      name,
      atSec: claudeRes.subagentAtSec?.[i] ?? null,
    })),
  });
  const charged = chargeAgainstCeiling({
    spentUsd: taskSpentUsd,
    costUsd: claudeRes.costUsd,
    ceilingUsd: MAX_USD_PER_TASK,
    assumedUsd: ASSUMED_USD,
  });
  taskSpentUsd = charged.spentUsd;
  budgetExhausted = charged.exhausted;
  console.log(
    `\nAttempt ${attempt} spend: ${describeSpend(sumSpend(attemptCosts.slice(-1), ASSUMED_USD))}` +
      ` · task so far: ${describeSpend(sumSpend(attemptCosts, ASSUMED_USD))}`,
  );
  if (budgetExhausted) {
    console.log(
      `This task has ${describeCeiling("AGENT_MAX_USD_PER_TASK", taskSpentUsd, MAX_USD_PER_TASK)}` +
        ` — no further attempt will start.`,
    );
  }

  // An account-level refusal is not this task's failure. Exit with a distinct code so the
  // drain can stop the whole run instead of marching every remaining task through failed/
  // for a reason none of them caused — which is exactly what happened when the session
  // limit was hit: three attempts each, $0.00 each, three tasks filed as failures.
  if (claudeRes.blocked) {
    console.error(`\nThe account cannot run right now: ${claudeRes.blocked}`);
    console.error("This task was not attempted. Nothing has been committed or moved.");
    // The refusal usually says when it lifts. Write that down so the next drain waits for
    // it instead of rediscovering the same refusal every minute — 1,986 such runs are on
    // record, 214 of them against one brief in a night.
    const until = recordAccountBlock(claudeRes.blocked);
    if (until) console.error(`The account recovers at ${until.toLocaleTimeString()}; drains will hold until then.`);
    finish("blocked", { attempts: attempt, failure: claudeRes.blocked });
    process.exit(EXIT_BLOCKED);
  }

  // Running out of turns is not the same as failing. An attempt stopped at the limit has
  // often already finished the work and merely kept narrating — one did, and discarding it
  // unverified threw away a correct, reviewable change that cost $22.82 and then blocked
  // every later drain with its own leftovers. So verify what it produced before judging it,
  // and fall through to the same acceptance path any other attempt takes.
  if (claudeRes.stoppedAtLimit) {
    console.error(`\nAttempt ${attempt} was stopped at ${claudeRes.turns} turns.`);
    console.error("Its work is left in the tree — the next attempt continues from it rather");
    console.error("than starting over, and verification decides whether it was enough.");
    console.error("Verifying what it produced before calling it a failure.");
    turnLimitNote =
      `The previous attempt was stopped after ${claudeRes.turns} turns, which is the ` +
      "per-attempt limit. It was not failing — it was not converging. Work in smaller " +
      "steps: make the change, verify, and delegate the reviews rather than re-reading the " +
      "same files. If the task genuinely cannot be done within that budget, say so and stop.";
    stoppedAtLimitThisAttempt = true;
  }

  if (claudeRes.status !== 0) {
    console.error(`\nClaude Code exited with status ${claudeRes.status}.`);
    feedback = `Claude Code itself exited non-zero (${claudeRes.status}).`;
    continue;
  }

  // A task that changed nothing has not done its job, and an empty diff sails through the
  // gate: nothing to typecheck, no live check demanded, exit 0, "Verified." That is how a
  // task gets filed as done having produced no files at all. Treat it as a failed attempt
  // and feed it back, so the agent gets told rather than the queue getting a lie.
  const produced = spawnSync("git", ["status", "--porcelain"], { encoding: "utf8" }).stdout ?? "";
  if (produced.trim() === "") {
    console.log(`\nAttempt ${attempt} produced no changes at all — not verifying an empty diff.`);
    feedback =
      "The working tree is unchanged: the previous attempt wrote nothing. If the task " +
      "cannot be done as stated — for example because it needs code from a dependency " +
      "that is not on this branch — say so explicitly instead of exiting quietly.";
    continue;
  }

  log(`Attempt ${attempt} — verifying`);
  const verifyArgs = [
    join(SCRIPTS, "verify.mjs"),
    "--port", argOf("verify-port", DEFAULT_VERIFY_PORT),
    // The branch this task was based on. Judging a worktree against
    // a stale base would report every file on the base branch as part of this change.
    "--base", argOf("verify-base", WORK_BRANCH),
  ];
  if (!flag("no-autostart")) verifyArgs.push("--autostart");

  const verify = run("node", verifyArgs, { stdio: "pipe" });
  const output = `${verify.stdout ?? ""}\n${verify.stderr ?? ""}`;
  process.stdout.write(output);
  writeFileSync(join(LOG_DIR, `${RUN_ID}-attempt-${attempt}.log`), output);

  if (verify.status === 0) {
    // A green gate proves the change builds and its tests pass. It cannot see a missing
    // isolation filter, a prompt that invites the model to invent, or an assertion deleted
    // to make the gate go green — which is exactly what a task under retry pressure is
    // tempted to do. So a review by an agent that did not write the code is required, and
    // required means checked: the brief asks, and this refuses.
    //
    // Detection is on the Task tool calls actually observed in the stream, not on the
    // session's own account of what it did. An agent that says it reviewed and did not is
    // the case this exists to catch.
    // Which departments this change actually belongs to is derived from the paths in the
    // diff, not from the brief's prose. A brief describes intent; the diff is what
    // happened, and a task that promised not to touch the schema and did must still answer
    // to the migrator.
    const changedPaths = (spawnSync("git", ["status", "--porcelain"], { encoding: "utf8" }).stdout ?? "")
      .split("\n")
      .map((l) => l.slice(3).trim())
      .filter(Boolean);

    // Before asking who reviewed it, ask whether there is an "it". Verification on an
    // untouched tree passes — of course it does, the branch is green — so an attempt that
    // discussed the task and wrote nothing arrives here looking exactly like a success. Five
    // did, and each was committed and filed as done: five commit subjects describing a
    // dashboard that had not changed by a line. A green gate over an empty diff is the one
    // result that must never be accepted.
    if (!carriesProductCode(changedPaths)) {
      console.log(`\nAttempt ${attempt} verified, but the working tree holds no change to accept.`);
      feedback =
        `\`${config.commands.verify}\` passed, but nothing was written: \`git status\` shows no changed ` +
        `file outside \`.agent-queue/\` and \`${config.docs.builds}/\`. The gate is green because \`${WORK_BRANCH}\` ` +
        "is green, not because this task was done.\n\n" +
        "Read the brief again and implement it. Edit the actual source files — a build " +
        "record, a plan, or a description of what you would do is not the change. If you " +
        `believe the work already exists on \`${WORK_BRANCH}\`, name the commit and the lines that ` +
        "satisfy each **Done when** bullet instead of finishing silently.";
      continue;
    }

    const owed = explainMissing(changedPaths, claudeRes.subagents);
    if (owed.length > 0) {
      const used = claudeRes.subagents.length
        ? `You delegated to: ${claudeRes.subagents.join(", ")}.`
        : "You delegated to nobody.";
      console.log(
        `\nAttempt ${attempt} verified but skipped ${owed.length} required department(s) — not accepting it.`,
      );
      for (const d of owed) console.log(`   missing: ${d.agent}  (${d.label})`);

      feedback =
        `The change passed \`${config.commands.verify}\`, but ${owed.length} department(s) that own ` +
        `part of this diff never saw it. ${used}\n\n` +
        owed
          .map(
            (d) =>
              `- \`${d.agent}\` (${d.label}) — ${d.why}\n` +
              `  required because this change touched: ${d.triggeredBy.join(", ")}`,
          )
          .join("\n") +
        "\n\nDo not redo the work. Delegate the diff you already have to each agent named " +
        "above with the Task tool, act on what they return, and then finish. A reviewing " +
        "agent reports; you make the changes it asks for.";
      continue;
    }

    // A skill is a written procedure, not an agent — and leaving it to judgement means the
    // one run that most needs `debug-answer`, the one about to blame a prompt for what is
    // really a cache hit, is exactly the run that will not think to load it.
    // Skills are a procedure, not an outcome. A run that reached the right result without
    // reading one has still reached the right result — rejecting it there discarded a
    // finished, four-reviewer-approved change that had already done what the skill
    // describes, at a cost of $240. So this reports and does not block.
    const owedSkills = explainMissingSkills(changedPaths, task, claudeRes.skills);
    if (owedSkills.length > 0) {
      console.log(`\nNote: ${owedSkills.length} skill(s) this change's shape usually calls for were not loaded:`);
      for (const sk of owedSkills) console.log(`   ${sk.skill} — ${sk.why}`);
      console.log("   Not blocking: the reviews below are what decide whether this lands.\n");
    }

    // Order matters as much as attendance. A reviewer reading a half-finished diff reports
    // findings the next edit would have removed anyway, and a specialist called in after the
    // reviewers is a specialist who did not do the work.
    const outOfOrder = sequenceProblems(changedPaths, claudeRes.subagents);
    if (outOfOrder.length > 0) {
      console.log(`\nAttempt ${attempt} used every department, but out of sequence.`);
      for (const p of outOfOrder) console.log(`   ${p}`);
      feedback =
        "Every required department saw this change, but not in the order the pipeline " +
        "requires:\n\n" +
        outOfOrder.map((p) => `- ${p}`).join("\n") +
        "\n\nThe order is: every specialist that writes finishes first, then " +
        expectedSequence(changedPaths)
          .filter((d) => d.phase === "review")
          .map((d) => d.agent)
          .join(" → ") +
        ".\n\nDo not redo the work. Run the reviews again on the diff you already have, in " +
        "that order, and act on what each returns.";
      continue;
    }

    log(`All ${requiredAgents(changedPaths).length} required department(s) signed off, in order — accepting attempt ${attempt}`);
    passed = true;
    break;
  }

  console.log(`\nVerification failed on attempt ${attempt}. Feeding the output back.`);
  feedback = output;
}

// The ceiling only *stopped* something if there was something left to stop. When the last
// permitted attempt happens to be the one that crosses the line, what ended the run is the
// attempt count, and calling that a budget stop sends whoever reads it to raise a ceiling
// that was never the problem — for any ceiling between two and three attempts' worth, every
// ordinary three-attempt failure would be mislabelled as running out of money.
if (!passed && budgetExhausted && attempt < MAX_ATTEMPTS) {
  // Out of money is not the same failure as out of ideas, and the two call for opposite
  // responses: this one needs a bigger ceiling or a smaller task, not a code fix. It is
  // recorded as its own outcome so `auto:status` can say which happened, and exits on its
  // own status so the drain can tell them apart without parsing this narration.
  //
  // Nothing is committed, exactly as for a verification failure — the tree is left for the
  // caller's not-landed path, which reverts it so the next task starts clean. A non-zero
  // exit is what puts the drain on that path.
  const reason =
    `Stopped on the per-task spend ceiling: ` +
    `${describeCeiling("AGENT_MAX_USD_PER_TASK", taskSpentUsd, MAX_USD_PER_TASK)} ` +
    `after ${attempt} attempt(s). The work did not verify, and no further attempt was started.`;
  console.error(`\n${reason}`);
  console.error(
    "Raise AGENT_MAX_USD_PER_TASK, or split the task, and requeue it. Nothing was committed.",
  );
  finish("budget", { attempts: attempt, failure: reason });
  process.exit(BUDGET_EXIT_CODE);
}

if (!passed) {
  // Before calling anything a failure, ask two questions the runner used to skip — and that
  // a person answered by hand five times in one day, finding finished work each time.

  const left = (spawnSync("git", ["status", "--porcelain"], { encoding: "utf8" }).stdout ?? "")
    .split("\n")
    .filter((l) => l.trim() && !l.slice(3).trim().startsWith(".agent-queue/"));

  // 1. Did anything actually run? An attempt with no turns and no cost never reached the
  //    model: a missing CLI, a usage limit, DNS. The task is untouched, so it belongs back
  //    in the queue rather than in the lane a person visits when work is broken.
  const spentOnTask = attemptCosts.reduce((n, c) => n + (typeof c === "number" ? c : 0), 0);
  const ranAtAll = attemptTurns.some((t) => t > 0) || spentOnTask > 0;
  if (!ranAtAll && left.length === 0) {
    console.error(`\nNothing ran: ${attempt} attempt(s), no turns, nothing spent.`);
    console.error("The environment stopped this, not the task. Leaving it untouched.");
    // There are two ways out of here marked BLOCKED, and recording the account's reset time
    // on only one of them is the same as not recording it: for 78 minutes this path fired
    // every 37 seconds, wrote no marker, and every drain went back round to meet the same
    // refusal. The reset time is in the feedback whichever path noticed it.
    const until = recordAccountBlock(feedback);
    if (until) console.error(`The account recovers at ${until.toLocaleTimeString()}; drains will hold until then.`);
    finish("blocked", { attempts: attempt, failure: feedback || "no attempt reached the model" });
    process.exit(EXIT_BLOCKED);
  }

  // 2. Is the work already done? An attempt stopped by a turn limit, a killed process or a
  //    dropped connection can be holding a finished change. Verification is the only honest
  //    way to tell, and it is cheap next to throwing the work away and rebuilding it.
  if (left.length > 0) {
    console.error(`\nGave up after ${MAX_ATTEMPTS} attempt(s), but ${left.length} file(s) are changed.`);
    console.error("Verifying them before calling this a failure — it may be finished work.\n");

    const salvage = run("node", [join(SCRIPTS, "verify.mjs"), "--autostart"], { stdio: "pipe" });
    process.stdout.write(`${salvage.stdout ?? ""}\n${salvage.stderr ?? ""}`);

    if (salvage.status === 0) {
      console.log("\nIt verifies. Accepting the work rather than discarding it.");
      passed = true;
    } else {
      console.error(
        "\nIt does not verify, so the change is genuinely unfinished. The tree is left as\n" +
          "it is: a later drain parks it in .agent-runs/interrupted/ rather than blocking.\n\n" +
          "  Read it:     git status && git diff\n" +
          "  Discard it:  git checkout -- . ':(exclude).agent-queue'",
      );
    }
  }

  if (!passed) {
    console.error(`\nGave up after ${MAX_ATTEMPTS} attempt(s). Verification never passed.`);
    console.error(`Output is in ${LOG_DIR}/.`);
    finish("failed", { attempts: attempt, failure: feedback });
    process.exit(1);
  }
}


log("Verified");

// --- commit ----------------------------------------------------------------------------
// Committing is not bookkeeping here, it is the mechanism. It moves this task's code onto
// the work branch where the next task in the queue can build on it, and it leaves the tree
// clean so that task can start. A verified task that stayed uncommitted would block the
// whole queue behind it.
//
// Pushing is a different question: work accumulates as local commits for the owner to
// review and push themselves, unless the operator set AGENT_AUTO_PUSH=1.
const subject = task.trim().split("\n")[0].slice(0, 72);

if (flag("no-commit")) {
  finish("verified", { attempts: attempt });
  console.log(`
Verified and left uncommitted on ${WORK_BRANCH} (--no-commit).

  Review it:  git status && git diff
  Commit it:  npm run auto:ship -- "${subject.replace(/"/g, '\\"')}"`);
  process.exit(0);
}

const shipArgs = [join(SCRIPTS, "auto-ship.mjs"), subject, "--commit-only"];
// The brief this run came from, so `queue -- audit` can pair the commit to it by trailer
// rather than by matching subject text.
if (taskFile) shipArgs.push("--task", basename(taskFile, ".md"));
const ship = runLive("node", shipArgs);
if (ship.status !== 0) {
  // Verified but uncommitted is a failure for this runner: the queue's next task would
  // start on a dirty tree and fold this work into its own commit.
  finish("failed", { attempts: attempt, failure: "verified, but the commit failed" });
  console.error(`\nThe task verified but could not be committed. ${WORK_BRANCH} is left dirty.`);
  process.exit(ship.status ?? 1);
}

finish("committed", { attempts: attempt });

// Push is off by default. AGENT_AUTO_PUSH=1 pushes the work branch after each commit.
// Scripts are not subject to Claude's Bash hook; this is the only path that pushes.
const autoPush = wantsAutoPush();
if (autoPush) {
  log(`Pushing ${WORK_BRANCH} (AGENT_AUTO_PUSH)`);
  const push = run("git", ["push", "origin", WORK_BRANCH]);
  if (push.status !== 0) {
    console.error(`\nCommitted on ${WORK_BRANCH}, but \`git push origin ${WORK_BRANCH}\` failed.`);
    console.error(push.stderr || push.stdout || "(no output)");
    console.error("The queue can continue — the next task builds on the local commit.");
    process.exit(0);
  }
  console.log(`
Committed and pushed to origin/${WORK_BRANCH}.

  Review:  git show --stat HEAD`);
  process.exit(0);
}

console.log(`
Committed on ${WORK_BRANCH}. Nothing was pushed (set AGENT_AUTO_PUSH=1 to push).

  Review:  git show --stat HEAD
  Push:    git push`);
process.exit(0);
