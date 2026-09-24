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
 *   .agent-queue/todo/     waiting
 *   .agent-queue/doing/    in flight (a crashed run leaves its file here)
 *   .agent-queue/done/     verified
 *   .agent-queue/failed/   attempted, never verified — the failure is in the journal
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
 * What it deliberately does NOT do: merge. Push is on by default after each verified
 * commit (`AGENT_AUTO_PUSH`, disable with `=0`). Merging a PR stays forever human.
 */
import { spawn, spawnSync } from "node:child_process";
import { findCommitFor, isDefinite } from "./lib/commit-pairing.mjs";
import { wantsRefuseDirtyStart } from "./lib/agent-flags.mjs";
import { hasSalvage, restoreSalvage } from "./lib/salvage.mjs";
import { carriesProductCodeFromShow } from "./lib/landed.mjs";
import { classifyDoneShip, commitShaFromRecord } from "./lib/shipped-on-main.mjs";
import {
  existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync, renameSync, rmSync,
  unlinkSync,
} from "node:fs";
import { join, dirname, resolve, basename, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";
import { hasExecutable } from "./lib/proc.mjs";
import { config, WORK_BRANCH } from "./lib/project-config.mjs";

/** Where landed tasks leave their build records, repository-relative. */
const BUILDS_REL = String(config.docs.builds).replace(/[\\/]+$/, "");
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
 * The lock is a file holding the owner's pid, created with the "wx" flag so creation is
 * itself the test. A run killed with SIGKILL leaves the file behind, so a lock whose pid is
 * no longer alive is treated as stale and taken over — otherwise one hard kill would wedge
 * the queue until someone deleted the file by hand.
 */
const LOCK = join(QUEUE, "drain.lock");

function alive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    // EPERM means the pid exists but belongs to someone else — still alive.
    return err.code === "EPERM";
  }
}

function acquireLock(what) {
  mkdirSync(QUEUE, { recursive: true });
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      writeFileSync(LOCK, `${process.pid}\n`, { flag: "wx" });
      const release = () => {
        try {
          if (readFileSync(LOCK, "utf8").trim() === String(process.pid)) unlinkSync(LOCK);
        } catch {
          // Already gone, or never ours to remove. Either way there is nothing to clean up.
        }
      };
      process.on("exit", release);
      for (const sig of ["SIGINT", "SIGTERM", "SIGHUP"]) {
        process.on(sig, () => {
          release();
          process.exit(130);
        });
      }
      return;
    } catch (err) {
      if (err.code !== "EEXIST") throw err;
      const holder = Number(readFileSync(LOCK, "utf8").trim());
      if (Number.isInteger(holder) && holder > 0 && alive(holder)) {
        console.error(
          `Another ${what} is already running (pid ${holder}).\n` +
            "Two at once race over the same task files, so this one is stopping instead.\n" +
            `Wait for it to finish, or if you are sure it is gone: rm ${LOCK.replace(ROOT, ".")}`,
        );
        process.exit(1);
      }
      // Stale: the holder is gone. Clear it and take the lock on the next pass.
      console.log(`Clearing a stale queue lock left by pid ${holder} (no longer running).`);
      try {
        unlinkSync(LOCK);
      } catch {
        // Someone else cleared it first; the retry will find out.
      }
    }
  }
  console.error("Could not take the queue lock. Try again.");
  process.exit(1);
}
const listLane = (lane) =>
  existsSync(laneDir(lane)) ? readdirSync(laneDir(lane)).filter((f) => f.endsWith(".md")).sort() : [];

/**
 * A task may declare `Depends-on: 01-foo, 02-bar` in its first few lines. It stays in
 * todo/ until every named task is in done/, which now means "already committed to the work branch",
 * so a dependent genuinely sees the code it was waiting for.
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
    .filter((d) => d && !/^none$/i.test(d));
}

/**
 * With every task committed to the work branch in place, "verified" and "available to the next
 * task" are the same event: agent-run.mjs commits before the task leaves doing/, so a task
 * sitting in done/ is a task whose code is already on the work branch for its dependents to build
 * against. done/ is therefore the whole answer.
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
function dependencyState(stem) {
  const done = listLane("done").some(
    (f) => f.replace(/\.md$/, "") === stem || f.includes(stem),
  );
  return done ? "landed" : "unbuilt";
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
const SKIP_SPECS = new Set(["README.md", "TEMPLATE.md"]);

const digest = (text) => createHash("sha1").update(text).digest("hex").slice(0, 12);
const loadPlanned = () => {
  try {
    return JSON.parse(readFileSync(PLANNED, "utf8"));
  } catch {
    return {};
  }
};

const PLAN_BRIEF = buildPlanBrief();

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
  const pending = readdirSync(SPECS)
    .filter((f) => f.endsWith(".md") && !SKIP_SPECS.has(f))
    .filter((f) => planned[f] !== digest(readFileSync(join(SPECS, f), "utf8")));

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
    const before = listLane("todo").length;

    // stream-json so planning is watchable as it happens. With text the log sits unchanged
    // for the whole call, and an unattended planner that shows nothing looks broken.
    const child = spawn(
      "claude",
      [
        "-p", `${PLAN_BRIEF}${body}`,
        "--permission-mode", "bypassPermissions",
        "--output-format", "stream-json", "--verbose",
      ],
      { cwd: ROOT, stdio: ["ignore", "pipe", "inherit"] },
    );
    const startedAt = new Date().toISOString();
    const rendered = renderStream(child.stdout);
    const status = await new Promise((r) => {
      child.on("error", () => r(1));
      child.on("close", (code) => r(code ?? 1));
    });
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
      outcome: res.status === 0 ? "planned" : "failed",
      ...journalCostFields([cost], ASSUMED_USD),
    });

    if (res.status !== 0) {
      console.error(`  ${file}: planning failed; leaving it unplanned so the next run retries.`);
      continue;
    }

    const added = listLane("todo").length - before;
    console.log(`  ${file} → ${added} task(s) queued`);
    // Recorded against the contents, so editing a spec re-plans it and an untouched one
    // is never planned twice.
    planned[file] = digest(body);
    writeFileSync(PLANNED, `${JSON.stringify(planned, null, 2)}\n`);
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
  const name = `${stamp}-${slugify(body.split("\n")[0])}.md`;
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
const RUN_LIMIT_PER_TASK = Number(process.env.AGENT_MAX_RUNS_PER_TASK ?? 3);

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
    clearTaskHistory(basename(file, ".md"));
    renameSync(join(laneDir("failed"), file), join(laneDir("todo"), file));
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
 * Beyond pairing records to SHAs, it asks whether the claimed ship is on the work branch at all —
 * an untracked build record or an `*(uncommitted)*` Commit cell used to fall through with
 * no class at all (tasks 92–98 once sat that way).
 *
 * Read-only by default. `--fix` repairs only mechanically recoverable strays; it never
 * moves a "says it shipped, the work branch does not have it" brief.
 */
if (action === "audit") {
  ensureLanes();
  // `--fix` repairs what is mechanically recoverable instead of printing it for a person.
  // Reporting a correct problem and then waiting is still a stopped pipeline: one finished
  // task sitting in the wrong lane held up eight dependents until somebody noticed by hand.
  const FIX = process.argv.includes("--fix");
  const buildsDir = join(ROOT, BUILDS_REL);
  const records = existsSync(buildsDir)
    ? readdirSync(buildsDir).filter((f) => f.endsWith(".md") && f !== "README.md")
    : [];

  const done = listLane("done");
  const unrecorded = [];
  const uncommitted = [];
  const bookkeepingOnly = [];
  // Claims in done/ whose work is not on the work branch — untracked build record, *(uncommitted)* cell,
  // or a SHA that is not an ancestor of HEAD. Distinct from the older **none** class below.
  const notOnMain = [];

  // Files under docs/builds/ that git has on HEAD. An untracked record on disk is the
  // lie this class exists to catch: readdir finds it, but the work branch does not hold it.
  const trackedBuilds = new Set(
    (git(["ls-files", BUILDS_REL]).stdout ?? "")
      .split("\n")
      .map((p) => p.trim())
      .filter(Boolean)
      .map((p) => (p.startsWith(`${BUILDS_REL}/`) ? p.slice(BUILDS_REL.length + 1) : p)),
  );

  for (const file of done) {
    const stem = file.replace(/\.md$/, "").replace(/^\d{4}-\d{2}-\d{2}T[\d-]+-/, "");
    const record = records.find((r) => r.includes(stem) || stem.includes(r.replace(/\.md$/, "")));
    if (!record) {
      unrecorded.push(file);
      continue;
    }
    const text = readFileSync(join(buildsDir, record), "utf8");
    // Preserve the historical **none** report class — do not fold it into not_on_main.
    if (/\*\*Commit\*\*\s*\|\s*\*\*none/.test(text)) {
      uncommitted.push({ file, record });
      continue;
    }

    const sha = commitShaFromRecord(text);
    const recordOnMain = trackedBuilds.has(record);
    const commitOnMain =
      Boolean(sha) &&
      git(["merge-base", "--is-ancestor", sha, "HEAD"]).status === 0;
    const ship = classifyDoneShip({
      hasRecord: true,
      recordOnMain,
      commitSha: sha,
      commitOnMain,
    });
    if (ship === "not_on_main") {
      notOnMain.push({ file, record, sha });
      continue;
    }

    // A commit that carries only the brief's own lane move and its build record is not the
    // work. Six tasks once landed exactly that way — subjects claiming real features, one of
    // them carrying any application code — and both the queue and this audit called it done.
    const touched = git(["show", "--name-only", "--format=", sha]).stdout;
    if (!carriesProductCodeFromShow(touched)) bookkeepingOnly.push({ file, record, sha });
  }

  // A task whose commit is already on the work branch but whose brief never left todo/ or failed/.
  // Every automatic path assumes the runner did the committing; nothing was watching for
  // work that landed some other way, which is exactly how this arose.
  const commits = (git(["log", "--no-merges", "--format=%H%x09%s%x09%B%x00"]).stdout ?? "")
    .split("\0")
    .map((r) => r.replace(/^\n/, ""))
    .filter(Boolean)
    .map((entry) => {
      const [sha, subject, message = ""] = entry.split("\t");
      return { sha, subject, message };
    });

  const strays = [];
  for (const lane of ["todo", "failed"]) {
    for (const file of listLane(lane)) {
      const brief = readFileSync(join(laneDir(lane), file), "utf8");
      const hit = findCommitFor({ taskFile: file, brief, commits });
      if (hit) strays.push({ lane, file, ...hit });
    }
  }

  console.log(`Audited ${done.length} task(s) in done/ against ${records.length} build record(s).\n`);

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
      for (const st of repairable) {
        const from = join(laneDir(st.lane), st.file);
        spawnSync(
          "node",
          [join(ROOT, "scripts", "record-build.mjs"), "--task", from, "--commit", st.sha],
          { cwd: ROOT, stdio: "ignore" },
        );
        renameSync(from, join(laneDir("done"), st.file));
        console.log(`  filed  ${st.file}  →  done/  against ${st.sha.slice(0, 7)}`);
      }
      const weak = strays.length - repairable.length;
      if (weak > 0) {
        console.log(`  left alone: ${weak} match(es) too weak to act on — check them by hand.`);
      }
    }
  }

  if (unrecorded.length && FIX) {
    console.log(`\nReconstructing ${unrecorded.length} missing build record(s) from history.`);
    spawnSync("node", [join(ROOT, "scripts", "record-build.mjs"), "--backfill"], {
      cwd: ROOT,
      stdio: "ignore",
    });
  }

  if (uncommitted.length) {
    console.log(`\n${uncommitted.length} task(s) filed as done with NO commit behind them:`);
    for (const u of uncommitted) console.log(`  ${u.file}  →  ${BUILDS_REL}/${u.record}`);
    console.log(
      "\nEach one released its dependents against code that is not on the work branch. Requeue the work\n" +
        "or delete the brief — leaving it claims something untrue about this repository.",
    );
    // Deliberately never repaired, with or without --fix. Requeueing rebuilds work that may
    // already exist under another subject; deleting the brief throws away a description
    // nobody else holds. Which of those is right is a judgement about whether the work is
    // still wanted, and a script that guesses it silently discards real work.
    if (FIX) console.log("`--fix` does not touch these: choosing between the two is yours.");
  }
  if (notOnMain.length) {
    console.log(
      `\n${notOnMain.length} task(s) say they shipped, and the work branch does not have them:`,
    );
    for (const n of notOnMain) {
      const why = !trackedBuilds.has(n.record)
        ? "build record not on the work branch"
        : n.sha
          ? `commit ${n.sha.slice(0, 7)} not on the work branch`
          : "no commit on the work branch";
      console.log(`  ${n.file}  →  ${BUILDS_REL}/${n.record}  (${why})`);
    }
    console.log(
      "\n`done/` released dependents against code that is not here. Do not requeue or commit\n" +
        "from `--fix`: moving the brief throws away a finished diff in the tree; committing\n" +
        "ships unreviewed work. Decide by hand.",
    );
    if (FIX) console.log("`--fix` leaves every brief in this class exactly where it is.");
  }
  if (bookkeepingOnly.length) {
    console.log(`\n${bookkeepingOnly.length} task(s) whose commit carries only queue bookkeeping:`);
    for (const b of bookkeepingOnly) console.log(`  ${b.file}  →  ${b.sha.slice(0, 8)}`);
    console.log(
      "\nThe brief moved and a build record was written, but no application file changed.\n" +
        "The feature these describe does not exist. Requeue them.",
    );
  }
  if (unrecorded.length) {
    console.log(`\n${unrecorded.length} task(s) in done/ with no build record at all:`);
    for (const u of unrecorded) console.log(`  ${u}`);
    console.log("\nRun `npm run record -- --backfill` to reconstruct them from history.");
  }
  if (
    !uncommitted.length &&
    !unrecorded.length &&
    !strays.length &&
    !notOnMain.length &&
    !bookkeepingOnly.length
  ) {
    console.log("Every finished task has a build record naming the commit that carries it.");
  }
  process.exit(uncommitted.length || bookkeepingOnly.length || notOnMain.length ? 1 : 0);
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
    for (const file of stranded) {
      renameSync(join(laneDir("doing"), file), join(laneDir("todo"), file));
      console.log(`requeued  ${file}`);
    }
    console.log(
      `\n${stranded.length} task(s) back in the queue. An interrupted task starts over, and ` +
        `any uncommitted work it left on the work branch is reverted when it runs again.`,
    );
  }
  spawnSync("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"], { stdio: "inherit" });
  process.exit(0);
}

if (action === "plan") {
  // planSpecs() writes new task files into todo/; a drain listing todo/ at the same moment
  // would see a half-written intake.
  acquireLock("drain");
  ensureLanes();
  // Awaited: planSpecs streams its planner output now, so an unawaited call would print the
  // queue listing before a single task file had been written.
  await planSpecs();
  spawnSync("node", [join(ROOT, "scripts", "agent-queue.mjs"), "list"], { stdio: "inherit" });
  process.exit(0);
}

if (action !== "drain") {
  console.error(`Unknown action "${action}". Use: add | list | plan | drain | resume | clean`);
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
// Two, matching agent-run's own default — this line was silently overriding it, so the
// evidence-based drop from three never reached a drained task. Of the tasks that needed a
// third attempt, the third did roughly a third of the total turns for a coin-flip chance of
// landing; a task that has failed twice needs its brief changed, not a third identical run.
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
    .filter((l) => l.trim() && !l.slice(3).trim().startsWith(".agent-queue/")));
  if (dirty.length === 0) return null;

  const parked = join(ROOT, ".agent-runs", "interrupted", slug);
  mkdirSync(parked, { recursive: true });

  // HEAD-relative and including staged changes, so one patch restores the whole tracked
  // state with `git apply`. Written first: everything after this line destroys it.
  const patch = spawnSync("git", ["diff", "HEAD", "--", ".", ":(exclude).agent-queue"], {
    cwd: ROOT, encoding: "utf8", maxBuffer: 64 * 1024 * 1024,
  }).stdout ?? "";
  if (patch.trim()) writeFileSync(join(parked, "tracked.patch"), patch);

  const untracked = dirty
    .filter((l) => l.startsWith("??"))
    .map((l) => l.slice(3).trim())
    .filter(Boolean);

  git(["reset", "--", ".", ":(exclude).agent-queue"]);
  git(["checkout", "--", ".", ":(exclude).agent-queue"]);

  for (const pathname of untracked) {
    const target = join(parked, pathname.replace(/[/\\]/g, "__"));
    try {
      renameSync(join(ROOT, pathname), target);
    } catch {
      // A directory, or already gone. The clean-tree check below reports what remains
      // rather than this silently pretending it was handled.
    }
  }

  return { dir: parked, patched: Boolean(patch.trim()), untracked: untracked.length };
}

function assertCleanMain() {
  const branch = (spawnSync("git", ["rev-parse", "--abbrev-ref", "HEAD"], {
    cwd: ROOT, encoding: "utf8",
  }).stdout ?? "").trim();
  if (branch !== WORK_BRANCH) {
    console.error(
      `The queue builds on ${WORK_BRANCH}, and this checkout is on "${branch}".\n` +
        `Switch to ${WORK_BRANCH} first:  git checkout ${WORK_BRANCH}`,
    );
    process.exit(1);
  }

  // Queue bookkeeping under .agent-queue/ does not count: this runs after the drain lock
  // is taken, and a task claimed by an interrupted earlier run may already have moved.
  const dirty = ((spawnSync("git", ["status", "--porcelain"], {
    cwd: ROOT, encoding: "utf8",
  }).stdout ?? "")
    .split("\n")
    .filter((l) => l.trim() && !l.slice(3).startsWith(".agent-queue/")));
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
      .filter((l) => l.trim() && !l.slice(3).trim().startsWith(".agent-queue/")));

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
 * The task is requeued, never resumed mid-flight: a dead run's partial edits are not
 * trustworthy, and assertCleanMain() below refuses to start while any of them remain.
 */
const stranded = listLane("doing");
if (stranded.length > 0) {
  console.log(`Requeuing ${stranded.length} task(s) left behind by a run that did not finish:`);
  for (const file of stranded) {
    renameSync(join(laneDir("doing"), file), join(laneDir("todo"), file));
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
  const detail = b.waitingOn.map((w) => `${w.dep} (not built yet)`).join(", ");
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
    const slug = slugify(basename(file, ".md").replace(/^[\d-T]+-/, ""));
    const taskPath = join(laneDir("doing"), file);
    renameSync(join(laneDir("todo"), file), taskPath);

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
      "--on-main",
    ];

    console.log(`\n[${index + 1}/${totalQueued}] ${slug}`);

    // Restore parked leftovers from a prior failed/blocked attempt *before* the runner
    // starts, so it sees a dirty tree of this task's work rather than a blank tree.
    if (hasSalvage(join(ROOT, ".agent-runs", "interrupted", slug))) {
      const restored = restoreSalvage(ROOT, slug);
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

    const child = spawn("node", args, { cwd: ROOT, stdio: "inherit" });

    child.on("close", (code) => {
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
        const started = priorTaskRuns(readFileSync(taskPath, "utf8").trim(), undefined, basename(file, ".md")).attempted;
        const lane = started >= RUN_LIMIT_PER_TASK ? "failed" : "todo";
        if (lane === "failed") {
          console.error(
            `${slug}: started ${started} time(s) already (AGENT_MAX_RUNS_PER_TASK=${RUN_LIMIT_PER_TASK}) — ` +
              "moved to failed/ instead of todo/. Clear the blocker, then `npm run queue -- retry`.",
          );
        }
        renameSync(taskPath, join(laneDir(lane), file));
        resolvePromise({ file, slug, ok: false, blocked: true, committed: false, commit: null });
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
      // The revert MUST exclude .agent-queue/. Its lanes are tracked, so a plain
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

      // Write the durable record before the brief leaves doing/: done/ is gitignored, so
      // this is the only moment the brief and the commit that satisfied it are both in
      // hand. record-build.mjs never exits non-zero on failure — an unrecorded build is a
      // documentation gap, not a reason to stop a drain.
      if (landed) {
        spawnSync(
          "node",
          [join(ROOT, "scripts", "record-build.mjs"), "--task", taskPath, "--commit", after],
          { cwd: ROOT, stdio: "inherit" },
        );
      }

      renameSync(taskPath, join(laneDir(landed ? "done" : "failed"), file));
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
const failed = results.filter((r) => !r.ok);
const budgeted = failed.filter((r) => r.stoppedOnBudget);

console.log(`\n${"=".repeat(70)}`);
console.log(
  `${ok.length} built and committed, ${failed.length - budgeted.length} failed` +
    `${budgeted.length ? `, ${budgeted.length} stopped on the per-task spend ceiling` : ""}.`,
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
  results.map((r) => ({ label: r.slug, ok: r.ok, spend: r.spend })),
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
// The exit status is the channel `loop.mjs` and the timer read: 3 means the ceiling stopped
// this drain, 1 means work failed, 0 means the queue was worked down. A budget stop takes
// precedence — it is the reason there is more to do, and the failures are listed above.
process.exit(budgetStop ? BUDGET_EXIT_CODE : failed.length > 0 ? 1 : 0);
