import { strict as assert } from "node:assert";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { landedStemsIn } from "../lib/landed-tasks.mjs";
import { nextNumber, planNameProblems, specSlug, specsChangedOn } from "../lib/planning.mjs";
import { ownBriefPath, stageable } from "../lib/stage-paths.mjs";
import { planDirName, plannedEntry, recordPathFor, renderRecord } from "../lib/build-record.mjs";
import { fsKey, listTasks, nameOf, qualify, scopeOf, stemOf } from "../lib/task-names.mjs";
import { validateConfig, config, workflowRefusal } from "../lib/project-config.mjs";

const entry = (path, worktree = "M", index = " ") => ({ index, worktree, path });
const SCOPE = "billing";

// --- task names ------------------------------------------------------------------------

test("a task's stem is its path below the lane: scope, then name", () => {
  assert.equal(stemOf(`${SCOPE}/01-endpoint.md`), `${SCOPE}/01-endpoint`);
  assert.equal(stemOf(`D:\\repo\\.agent-queue\\doing\\${SCOPE}\\01-endpoint.md`), `${SCOPE}/01-endpoint`);
  assert.equal(stemOf(`.agent-queue/todo/adhoc/2026-10-06T10-00-00-fix.md`), "adhoc/2026-10-06T10-00-00-fix");
  assert.equal(stemOf("01-legacy.md"), "01-legacy");
  assert.equal(stemOf("/tmp/briefs/one-off.md"), "one-off");
  assert.equal(scopeOf(`${SCOPE}/01-endpoint`), SCOPE);
  assert.equal(nameOf(`${SCOPE}/01-endpoint`), "01-endpoint");
  assert.equal(scopeOf("01-legacy"), "");
  assert.equal(fsKey(`${SCOPE}/01-endpoint`), `${SCOPE}__01-endpoint`);
});

test("a bare dependency is the sibling in the same plan; a scoped one stands as written", () => {
  assert.equal(qualify("01-endpoint", `${SCOPE}/02-screen`), `${SCOPE}/01-endpoint`);
  assert.equal(qualify("01-endpoint.md", `${SCOPE}/02-screen`), `${SCOPE}/01-endpoint`);
  assert.equal(qualify("other-plan/01-x", `${SCOPE}/02-screen`), "other-plan/01-x");
  assert.equal(qualify("01-a", "02-legacy"), "01-a");
});

test("a lane lists loose files and one level of scope folders", () => {
  const dir = mkdtempSync(join(tmpdir(), "lanes-"));
  try {
    mkdirSync(join(dir, SCOPE));
    mkdirSync(join(dir, "adhoc"));
    writeFileSync(join(dir, SCOPE, "01-a.md"), "x");
    writeFileSync(join(dir, SCOPE, "notes.txt"), "x");
    writeFileSync(join(dir, "adhoc", "2026-10-06T10-00-00-fix.md"), "x");
    writeFileSync(join(dir, "01-legacy.md"), "x");
    writeFileSync(join(dir, ".gitkeep"), "");
    assert.deepEqual(listTasks(dir), [`${SCOPE}/01-a.md`, "01-legacy.md", "adhoc/2026-10-06T10-00-00-fix.md"].sort());
    assert.deepEqual(listTasks(join(dir, "missing")), []);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// --- staging --------------------------------------------------------------------------

test("a task commit stages its code and its own brief leaving todo/, nothing else from the queue", () => {
  const entries = [
    entry("src/app.ts"),
    entry(`.agent-queue/todo/${SCOPE}/01-endpoint.md`, "D"),
    entry(`.agent-queue/todo/${SCOPE}/03-failed-earlier.md`, "D"),
    entry(".agent-queue/todo/other-plan/01-endpoint.md", "D"),
    entry(".agent-queue/planned.json"),
  ];
  assert.deepEqual(
    stageable(entries, `${SCOPE}/01-endpoint`).map((e) => e.path),
    ["src/app.ts", `.agent-queue/todo/${SCOPE}/01-endpoint.md`],
  );
});

test("without a task, nothing under .agent-queue/ is staged", () => {
  const entries = [entry("src/app.ts"), entry(".agent-queue/todo/x.md", "D")];
  assert.deepEqual(stageable(entries, "").map((e) => e.path), ["src/app.ts"]);
});

test("build output is never staged", () => {
  const entries = [entry("src/web/.next/x.js"), entry("src/backend/bin/Debug/a.dll"), entry("src/web/node_modules")];
  assert.deepEqual(stageable(entries, "t"), []);
});

test("the own-brief path ignores a .md suffix on the stem", () => {
  assert.equal(ownBriefPath(`${SCOPE}/01-endpoint.md`), `.agent-queue/todo/${SCOPE}/01-endpoint.md`);
});

// --- landed from history ---------------------------------------------------------------

test("landed stems come from every Task: line, scoped, squash bodies included", () => {
  const log =
    `Add the endpoint\n\nVerified.\n\nTask: ${SCOPE}/01-endpoint\n\n` +
    `Add billing (#7)\n\n* one\n\nTask: ${SCOPE}/02-screen\n* two\n\nTask: adhoc/2026-10-06T10-00-00-fix\n`;
  assert.deepEqual(
    [...landedStemsIn(log)].sort(),
    [`${SCOPE}/01-endpoint`, `${SCOPE}/02-screen`, "adhoc/2026-10-06T10-00-00-fix"].sort(),
  );
  assert.equal(landedStemsIn("").size, 0);
});

// --- planning --------------------------------------------------------------------------

test("a spec's slug is its lowercased stem", () => {
  assert.equal(specSlug("billing-export.md"), "billing-export");
  assert.equal(specSlug("Billing Export v2.md"), "billing-export-v2");
  assert.equal(specSlug("---.md"), "spec");
});

test("planned files must sit in the plan's own folder, named NN-short-slug", () => {
  const problems = planNameProblems({
    added: [`${SCOPE}/01-endpoint.md`, "01-loose.md", "other-plan/02-x.md", `${SCOPE}/2-screen.md`, `${SCOPE}/03-ok-too.md`],
    scope: SCOPE,
  });
  assert.deepEqual(problems.map((p) => p.split(":")[0]), ["01-loose.md", "other-plan/02-x.md", `${SCOPE}/2-screen.md`]);
});

test("planning a spec again may not reuse a name its folder already used, and numbers past it", () => {
  const problems = planNameProblems({
    added: [`${SCOPE}/01-endpoint.md`, `${SCOPE}/03-export.md`],
    scope: SCOPE,
    taken: ["01-endpoint", "02-screen"],
  });
  assert.deepEqual(problems.map((p) => p.split(":")[0]), [`${SCOPE}/01-endpoint.md`]);
  assert.equal(nextNumber(["01-endpoint", "02-screen"]), "03");
  assert.equal(nextNumber([]), "01");
});

test("a spec named adhoc does not share the hand-queued tasks' scope", () => {
  assert.equal(specSlug("adhoc.md"), "adhoc-spec");
});

test("team intake considers only top-level spec files this branch changed", () => {
  const specs = specsChangedOn([
    "specs/billing.md",
    "specs\\reports.md",
    "\"specs/with space.md\"",
    "specs/archive/old.md",
    "specs/notes.txt",
    "src/app.ts",
    "",
  ]);
  assert.deepEqual([...specs].sort(), ["billing.md", "reports.md", "with space.md"]);
});

// --- workflow ----------------------------------------------------------------------------

test("solo is the default workflow and the committed config is valid", () => {
  assert.equal(config.project.workflow, "solo");
  assert.deepEqual(validateConfig(config), []);
});

test("a workflow that is neither solo nor team is a config error", () => {
  const problems = validateConfig({ ...config, project: { ...config.project, workflow: "pairs" } });
  assert.ok(problems.some((p) => p.startsWith("project.workflow")));
});

test("team refuses the base branch; solo and other branches are allowed", () => {
  assert.match(workflowRefusal({ workflow: "team", branch: "main", base: "main" }), /git switch -c/);
  assert.equal(workflowRefusal({ workflow: "team", branch: "feat/x", base: "main" }), null);
  assert.equal(workflowRefusal({ workflow: "solo", branch: "main", base: "main" }), null);
  assert.equal(workflowRefusal({ workflow: "team", branch: "", base: "main" }), null);
});

// --- build records -----------------------------------------------------------------------

const at = new Date("2026-10-06T14:03:27.512Z");

test("a planning's record directory is its start time, to the second, then the spec's slug", () => {
  assert.equal(planDirName("billing-export", at), "2026-10-06T14-03-27-billing-export");
});

test("a spec's tasks record into the directory its planning named in planned.json", () => {
  const planned = {
    "billing.md": { hash: "a", builds: "2026-10-06T09-00-00-billing" },
    "billing-export.md": { hash: "b", builds: "2026-10-01T09-00-00-billing-export" },
  };
  assert.equal(recordPathFor(`${SCOPE}/01-endpoint`, { planned, date: at }), "2026-10-06T09-00-00-billing/01-endpoint.md");
  assert.equal(
    recordPathFor("billing-export/01-x", { planned, date: at }),
    "2026-10-01T09-00-00-billing-export/01-x.md",
  );
});

test("without a planned.json entry, a spec reuses its newest record directory, else starts one", () => {
  const existing = ["2026-09-01T00-00-00-billing", "2026-09-05T00-00-00-billing", "2026-09-09T00-00-00-billing-export", "adhoc"];
  assert.equal(recordPathFor(`${SCOPE}/02-screen`, { existing, date: at }), "2026-09-05T00-00-00-billing/02-screen.md");
  assert.equal(recordPathFor("reports/01-x", { existing, date: at }), "2026-10-06T14-03-27-reports/01-x.md");
});

test("a task with no spec is adhoc, its record led by a date-time", () => {
  assert.equal(recordPathFor("adhoc/2026-10-05T08-00-00-fix-typo", { date: at }), "adhoc/2026-10-05T08-00-00-fix-typo.md");
  assert.equal(recordPathFor("01-legacy", { date: at }), "adhoc/2026-10-06T14-03-27-01-legacy.md");
});

test("planned.json entries written before scopes existed still read", () => {
  assert.deepEqual(plannedEntry("abc123"), { hash: "abc123", builds: null });
  assert.deepEqual(plannedEntry({ hash: "h", builds: "d" }), { hash: "h", builds: "d" });
});

test("the record carries the subject, the task, the files and the brief verbatim", () => {
  const text = renderRecord({
    stem: `${SCOPE}/01-endpoint`,
    brief: "Add the export endpoint.\r\nDepends-on: none\r\n\r\n## Scope\r\n- one\r\n",
    date: at,
    files: [{ file: "src/a.ts", add: "3", del: "1" }],
  });
  assert.match(text, /^# Add the export endpoint\n/);
  assert.ok(text.includes(`| **Task** | \`${SCOPE}/01-endpoint\` |`));
  assert.match(text, /\| \*\*Landed\*\* \| 2026-10-06 \|/);
  assert.match(text, /\| `src\/a\.ts` \| 3 \| 1 \|/);
  assert.match(text, /## The brief this was built from\n\nDepends-on: none\n\n## Scope\n- one\n$/);
});

test("a saved spec is not in the way of the drain that plans it", async () => {
  const { isPlannableSpecPath } = await import("../lib/planning.mjs");
  assert.equal(isPlannableSpecPath("?? specs/billing.md"), true);
  assert.equal(isPlannableSpecPath(" M specs/billing.md"), true);
  assert.equal(isPlannableSpecPath("specs\\billing.md"), true);
  assert.equal(isPlannableSpecPath('?? "specs/two words.md"'), true);
  // Its neighbours still count: the folder's own docs, nested files, anything else.
  assert.equal(isPlannableSpecPath(" M specs/README.md"), false);
  assert.equal(isPlannableSpecPath("?? specs/TEMPLATE.md"), false);
  assert.equal(isPlannableSpecPath("?? specs/drafts/x.md"), false);
  assert.equal(isPlannableSpecPath(" M src/specs/x.md"), false);
  assert.equal(isPlannableSpecPath("?? specs/notes.txt"), false);
});

test("a spec hashes the same whatever its line endings", async () => {
  const { specDigest } = await import("../lib/planning.mjs");
  assert.equal(specDigest("# A\r\n\r\nbody\r\n"), specDigest("# A\n\nbody\n"));
  assert.notEqual(specDigest("# A\n"), specDigest("# B\n"));
});

test("a spec's failed plans are counted per version, and a success is not a failure", async () => {
  const { failedPlanAttempts } = await import("../lib/planning.mjs");
  const run = (outcome, specHash, file = "billing.md") => ({ kind: "plan", task: `plan spec: ${file}`, specHash, outcome });
  const runs = [run("failed", "aaa"), run("rejected", "aaa"), run("planned", "aaa"), run("failed", "bbb"), run("failed", "aaa", "other.md")];
  assert.equal(failedPlanAttempts(runs, "billing.md", "aaa"), 2);
  assert.equal(failedPlanAttempts(runs, "billing.md", "bbb"), 1);
  assert.equal(failedPlanAttempts(runs, "billing.md", "ccc"), 0);
});

test("what a planner may leave behind is the queue and the specs, and nothing else", async () => {
  const { strayPlanPaths } = await import("../lib/planning.mjs");
  assert.deepEqual(
    strayPlanPaths(["src/wip.ts"], ["src/wip.ts", ".agent-queue/todo/x/01-a.md", "specs/x.md", "src/api.ts"]),
    ["src/api.ts"],
  );
  assert.deepEqual(strayPlanPaths([], []), []);
});

test("a re-plan is told which old tasks it replaces and which stay", async () => {
  const { buildPlanBrief } = await import("../lib/brief-plan.mjs");
  const brief = buildPlanBrief({ scope: SCOPE, taken: ["01-api", "02-page", "03-docs"], replaced: ["02-page", "03-docs"] });
  assert.match(brief, /02-page, 03-docs were planned from the old version/);
  assert.match(brief, /01-api already exist and stay/);
  assert.match(brief, /number the new tasks from 04/);
  // A first plan says nothing about earlier ones.
  assert.doesNotMatch(buildPlanBrief({ scope: SCOPE }), /planned before/);
});
