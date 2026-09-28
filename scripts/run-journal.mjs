#!/usr/bin/env node
/**
 * The autonomous loop's memory.
 *
 *   npm run auto:status            # what has been attempted, and how it went
 *   npm run auto:status -- --task "fix empty answers"
 *
 * Without this every run starts from zero: the same task fails the same way on Tuesday
 * that it failed on Monday, and nobody finds out because each run's output scrolls past.
 * One JSON line per run in .agent-runs/journal.jsonl, appended and never rewritten, so a
 * crashed run cannot corrupt the history.
 *
 * Also imported by agent-run.mjs, which reads prior failures for a task and puts them in
 * the brief — the loop's one form of learning between invocations.
 */
import { appendFileSync, readFileSync, existsSync, mkdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import {
  DEFAULT_ASSUMED_USD,
  combineSpend,
  describeSpend,
  formatUsd,
  parseAssumedUsd,
  spendOfRun,
  sumSpend,
} from "./lib/spend.mjs";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
export const LOG_DIR = join(ROOT, ".agent-runs");
const JOURNAL = join(LOG_DIR, "journal.jsonl");

/** Normalised so the same task worded slightly differently still matches. */
export function taskKey(task) {
  return task.toLowerCase().replace(/[^a-z0-9]+/g, " ").trim().split(" ").filter(Boolean).join(" ");
}

export function appendRun(entry) {
  if (!existsSync(LOG_DIR)) mkdirSync(LOG_DIR, { recursive: true });
  appendFileSync(JOURNAL, `${JSON.stringify(entry)}\n`);
}

export function readRuns() {
  if (!existsSync(JOURNAL)) return [];
  return readFileSync(JOURNAL, "utf8")
    .split("\n")
    .filter(Boolean)
    .map((line) => {
      try {
        return JSON.parse(line);
      } catch {
        return null;
      }
    })
    .filter(Boolean);
}

/**
 * Journal kind meaning "a person has looked at this task and cleared it".
 *
 * It rides on the journal rather than on a file beside it for one reason: the journal is
 * append-only and never rewritten, so a reset cannot be lost to a crash mid-write, and the
 * history it clears and the clearing itself stay in one order that can be read back.
 */
const RESET_KIND = "budget-reset";

/**
 * Forget what a task has spent so far, because its blocker has been cleared.
 *
 * Called by `queue -- retry`, which is already the gesture that means "a person looked".
 * Nothing automatic may call this — a loop that could clear its own ceiling does not have
 * one.
 */
export function clearTaskHistory(slug) {
  if (!slug) return;
  appendRun({ kind: RESET_KIND, taskSlug: slug, at: new Date().toISOString() });
}

/** A task's subject line, normalised — the part of a brief that survives editing its body. */
export function subjectKey(task) {
  return taskKey(String(task ?? "").split("\n", 1)[0] ?? "");
}

/**
 * Everything the journal already holds about *this* task, across every process that has
 * attempted it.
 *
 * The per-task spend ceiling used to be per-*process*: each `agent-run.mjs` started its
 * counter at zero, so a task the drain restarted six times could spend six times the
 * ceiling and never cross it once. That is not a hypothetical — one brief restarted thirty
 * times and cost $108.44 with a $50 ceiling in force, because no single run ever reached
 * $50. A ceiling that resets whenever the thing it bounds is retried is not a ceiling.
 *
 * **Identity is the brief's slug, then its subject line — never its body.** Matching whole
 * briefs was the obvious thing and it does not work: a brief gains a `Depends-on:` or has a
 * paragraph sharpened between runs, and the task's own history stops being its own. That
 * exact case is why this is written down — the $108 brief had grown by 1,500 characters, so
 * a body match found none of its six runs. The slug is journalled from the queue's file
 * name and changes only when the file is renamed; the subject is what the file was named
 * from, and covers entries written before the slug was recorded.
 *
 * Planning entries are excluded. They belong to a spec, not to any one task, and are
 * already bounded by the per-drain ceiling.
 */
export function taskRunsIn(entries, task, assumedUsd = DEFAULT_ASSUMED_USD, slug = null) {
  const subject = subjectKey(task);
  if (!subject && !slug) return { runs: 0, attempted: 0, spend: combineSpend([]) };
  const list = entries ?? [];
  const isMine = (r) => {
    if (!r) return false;
    if (slug && r.taskSlug) return r.taskSlug === slug;
    return Boolean(subject) && subjectKey(r.task) === subject;
  };
  // History is counted from the last time a person cleared this task, not from the
  // beginning of the journal. Without that seam the first task to reach the ceiling is
  // stranded for good — a guard that can only ever refuse would stall the loop worse than
  // the runaway it was written to stop, and would be switched off within a week.
  const cleared = list.map((r, i) => (r?.kind === RESET_KIND && isMine(r) ? i : -1)).reduce((a, b) => Math.max(a, b), -1);
  const mine = list.slice(cleared + 1).filter((r) => r && r.kind !== "plan" && r.kind !== RESET_KIND && isMine(r));
  // A run that recorded no cost is charged one unmeasured call, not zero — the same rule
  // `spendOfEntries` applies, and for the same reason: a run that left no bill behind is
  // far more often one that died badly than one that was free.
  const spends = mine.map((r) => {
    const s = spendOfRun(r, assumedUsd);
    return s.recorded === false ? sumSpend([null], assumedUsd) : s;
  });
  // Runs that actually took a swing at the task, as opposed to runs the account refused
  // before anything could happen. A session limit produces a run that lasted two seconds,
  // reached one turn and spent nothing, and it says something about the account rather than
  // about this brief — the drain's own comment has always said so. Counting those against a
  // task is how three briefs reached failed/ overnight for a reason none of them caused.
  const attempted = mine.filter((r, i) => {
    const sum = (r.turns ?? []).reduce((a, b) => a + (Number(b) || 0), 0);
    return spends[i].usd > 0 || sum > 1;
  }).length;
  return { runs: mine.length, attempted, spend: combineSpend(spends) };
}

/** `taskRunsIn` against the journal on disk. */
export function priorTaskRuns(task, assumedUsd = DEFAULT_ASSUMED_USD, slug = null) {
  return taskRunsIn(readRuns(), task, assumedUsd, slug);
}


/**
 * Prior runs of the same task that did not reach a verified state. Overlap is measured on
 * words rather than exact text, because a task re-queued by hand is rarely re-typed
 * identically.
 */
export function priorFailures(task, limit = 3) {
  const words = new Set(taskKey(task).split(" ").filter((w) => w.length > 3));
  if (words.size === 0) return [];

  return readRuns()
    // Planning entries record what `queue -- plan` spent; they are not attempts at any
    // task, so a failed one must never be fed back to an unrelated task as its own history.
    .filter((r) => r.kind !== "plan")
    .filter((r) => r.outcome && r.outcome !== "verified" && r.outcome !== "shipped")
    .filter((r) => {
      const other = new Set(taskKey(r.task ?? "").split(" ").filter((w) => w.length > 3));
      if (other.size === 0) return false;
      const shared = [...words].filter((w) => other.has(w)).length;
      return shared / Math.max(words.size, other.size) >= 0.6;
    })
    .slice(-limit);
}

/**
 * A short brief fragment describing what already happened, for the next attempt's prompt.
 *
 * A run stopped by a spend ceiling is included — the next attempt should know the task has
 * been expensive — but it is not described as a failed *approach*. Nothing judged the
 * approach: the run was cut off on cost, and telling the next attempt to abandon a method
 * that may have been working is how a task gets rewritten worse and costs more the second
 * time. The closing instruction is therefore only given when something actually failed.
 */
export function priorFailureBrief(task) {
  const prior = priorFailures(task);
  if (prior.length === 0) return "";

  const lines = prior.map((r) => {
    const when = r.finishedAt ?? r.startedAt ?? "unknown time";
    const why = (r.failure ?? "no detail recorded").slice(0, 400);
    return r.outcome === "budget"
      ? `- ${when} on branch ${r.branch ?? "?"}: stopped on its spend ceiling after ` +
        `${r.attempts ?? "?"} attempt(s), so the work was never judged. ${why}`
      : `- ${when} on branch ${r.branch ?? "?"} after ${r.attempts ?? "?"} attempt(s): ${why}`;
  });

  const anyFailed = prior.some((r) => r.outcome !== "budget");
  const advice = anyFailed
    ? `Do not repeat a failed *approach*. If prior work may still be in
.agent-runs/interrupted/ or the working tree, the runner restores and verifies it first —
prefer finishing that diff over rewriting the feature. If the cause was environmental
(session limit, network) rather than a code defect, say so plainly.`
    : `Nothing above says the approach was wrong, only that it ran out of budget. Work
economically — read what you need and no more — and say plainly in your final message if the
task cannot be finished within one run.`;

  return `
This task has been attempted before and did not reach a verified state:

${lines.join("\n")}

${advice}
`;
}

// --- CLI ------------------------------------------------------------------------------
if (process.argv[1] && process.argv[1].endsWith("run-journal.mjs")) {
  const argv = process.argv.slice(2);
  const i = argv.indexOf("--task");
  const filter = i >= 0 ? argv[i + 1] : null;

  const runs = readRuns().filter((r) => !filter || taskKey(r.task ?? "").includes(taskKey(filter)));

  if (runs.length === 0) {
    console.log("No runs recorded yet. `npm run auto -- \"<task>\"` writes the first one.");
    process.exit(0);
  }

  // `budget` is its own outcome, not a kind of failure: "ran out of money" and "could not
  // make the tests pass" call for opposite responses from whoever reads this — one needs a
  // bigger ceiling or a smaller task, the other needs the code fixed.
  const mark = {
    verified: "✓", shipped: "⇪", planned: "◇", failed: "✗", abandoned: "–", budget: "○",
  };
  // Scripts read the environment directly. This only affects how a call that reported no
  // cost is charged; a measured run reads the same whatever it is set to.
  const assumedUsd = parseAssumedUsd(process.env.AGENT_ASSUMED_USD_PER_CALL);
  const listed = runs.slice(-25);
  const spends = listed.map((r) => spendOfRun(r, assumedUsd));

  console.log(`${runs.length} run(s):\n`);
  listed.forEach((r, i) => {
    const head = `${mark[r.outcome] ?? "?"} ${(r.finishedAt ?? r.startedAt ?? "").slice(0, 19)}`;
    const cost = spends[i].recorded ? formatUsd(spends[i].usd) : "unknown";
    console.log(
      `${head}  ${(r.branch ?? "-").padEnd(34)} ${r.attempts ?? "?"} attempt(s)  ${cost.padStart(8)}` +
        `${spends[i].assumedCalls ? " (assumed)" : ""}`,
    );
    console.log(`   ${(r.task ?? "").split("\n")[0].slice(0, 96)}`);
    if (r.failure) console.log(`   └─ ${r.failure.split("\n")[0].slice(0, 96)}`);
  });

  // Planning calls are journal entries too — they spend real money — but they are not
  // attempts at a task, so they are counted apart from the verified/failed tally.
  const tasks = runs.filter((r) => r.kind !== "plan");
  const plans = runs.length - tasks.length;
  const failed = tasks.filter((r) => r.outcome === "failed").length;
  const budget = tasks.filter((r) => r.outcome === "budget").length;
  console.log(
    `\n${tasks.length - failed - budget} verified, ${failed} failed` +
      `${budget ? `, ${budget} stopped on a spend ceiling` : ""}` +
      `${plans ? `, plus ${plans} planning call(s)` : ""}.`,
  );
  if (budget) {
    console.log(
      "A ceiling stop is not a verification failure: the work may be sound and merely larger\n" +
        "than AGENT_MAX_USD_PER_TASK. Raise the ceiling or split the task, then requeue it.",
    );
  }

  // A run written before the loop kept a ledger carries no cost field at all, and reads as
  // unknown rather than as $0.00 — reporting the loop's whole history as free would be the
  // one number here nobody should trust.
  // combineSpend carries the runs that recorded nothing through as `unrecorded`, so the
  // figure printed here can never read as covering more runs than it counted.
  const total = combineSpend(spends);
  const counted = spends.length - (total.unrecorded ?? 0);
  console.log(
    total.recorded === false
      ? `\nSpend across the ${listed.length} run(s) listed: not recorded for any of them.`
      : `\nSpend across ${counted} of the ${listed.length} run(s) listed: ${describeSpend(total)}.`,
  );
}
