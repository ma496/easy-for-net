import { test } from "node:test";
import assert from "node:assert/strict";
import { taskKey, taskRunsIn } from "../run-journal.mjs";

const BRIEF = "Push a filtered selection to HubSpot in one action.";
const OTHER = "Render a posting's headings and bullets instead of one block.";

const run = (task, costUsd, extra = {}) => ({
  id: `run-${Math.random()}`,
  task,
  attemptCostsUsd: costUsd === null ? undefined : [costUsd],
  costUsd: costUsd ?? undefined,
  ...extra,
});

test("a task that has never run has spent nothing", () => {
  const h = taskRunsIn([run(OTHER, 4)], BRIEF);
  assert.equal(h.runs, 0);
  assert.equal(h.spend.usd, 0);
});

test("spend is summed across every earlier run of the same task", () => {
  // The $108 case: six runs, each well under a $50 ceiling, together far over it.
  const entries = [run(BRIEF, 18), run(OTHER, 40), run(BRIEF, 18), run(BRIEF, 18)];
  const h = taskRunsIn(entries, BRIEF);
  assert.equal(h.runs, 3);
  assert.equal(h.spend.usd, 54);
});

test("whitespace and punctuation in the brief do not split its history", () => {
  const h = taskRunsIn([run(`  ${BRIEF.replace(".", "")}\n`, 5)], BRIEF);
  assert.equal(h.runs, 1);
});

test("planning entries belong to a spec, not to a task", () => {
  const h = taskRunsIn([run(BRIEF, 9, { kind: "plan" })], BRIEF);
  assert.equal(h.runs, 0);
  assert.equal(h.spend.usd, 0);
});

test("a run whose cost could not be read is charged, not skipped", () => {
  // Otherwise the runs most likely to be misbehaving are exactly the ones that slip past.
  const h = taskRunsIn([{ id: "x", task: BRIEF }], BRIEF, 2);
  assert.equal(h.runs, 1);
  assert.ok(h.spend.usd >= 2, `expected the assumed figure to be charged, got ${h.spend.usd}`);
});

test("an empty brief matches nothing rather than everything", () => {
  assert.equal(taskKey("   "), "");
  assert.equal(taskRunsIn([run(BRIEF, 3)], "   ").runs, 0);
});

test("the slug is the identity, so editing a brief's body does not lose its history", () => {
  // The $108 brief grew by 1,500 characters between runs; a body match found none of them.
  const entries = [
    { id: "a", task: `${BRIEF}\n\nfirst wording`, taskSlug: "78-push", costUsd: 60 },
  ];
  const h = taskRunsIn(entries, `${BRIEF}\n\nrewritten, twice as long`, 1, "78-push");
  assert.equal(h.runs, 1);
  assert.equal(h.spend.usd, 60);
});

test("entries written before slugs were journalled still match on the subject line", () => {
  const entries = [{ id: "a", task: `${BRIEF}\n\nold body`, costUsd: 7 }];
  const h = taskRunsIn(entries, `${BRIEF}\n\nnew body`, 1, "78-push");
  assert.equal(h.runs, 1);
});

test("a cleared task starts from zero again", () => {
  const entries = [
    { id: "a", task: BRIEF, taskSlug: "78-push", costUsd: 60 },
    { kind: "budget-reset", taskSlug: "78-push" },
    { id: "b", task: BRIEF, taskSlug: "78-push", costUsd: 4 },
  ];
  const h = taskRunsIn(entries, BRIEF, 1, "78-push");
  assert.equal(h.runs, 1);
  assert.equal(h.spend.usd, 4);
});

test("clearing one task does not clear another's", () => {
  const entries = [
    { id: "a", task: OTHER, taskSlug: "79-other", costUsd: 60 },
    { kind: "budget-reset", taskSlug: "78-push" },
  ];
  const h = taskRunsIn(entries, OTHER, 1, "79-other");
  assert.equal(h.runs, 1);
  assert.equal(h.spend.usd, 60);
});

test("a run the account refused is not a run this task spent", () => {
  // A session limit: two seconds, one turn, nothing spent. It says something about the
  // account, not about the brief — three tasks reached failed/ overnight for want of this.
  const entries = [
    { id: "a", task: BRIEF, taskSlug: "79-x", costUsd: 0, attemptCostsUsd: [0], turns: [1] },
    { id: "b", task: BRIEF, taskSlug: "79-x", costUsd: 0, attemptCostsUsd: [0], turns: [1] },
    { id: "c", task: BRIEF, taskSlug: "79-x", costUsd: 0, attemptCostsUsd: [0], turns: [1] },
  ];
  const h = taskRunsIn(entries, BRIEF, 1, "79-x");
  assert.equal(h.runs, 3, "they are still runs");
  assert.equal(h.attempted, 0, "but none of them attempted the task");
});

test("a run that did real work before blocking still counts", () => {
  const entries = [
    { id: "a", task: BRIEF, taskSlug: "79-x", costUsd: 23.37, attemptCostsUsd: [23.37], turns: [259, 364, 170] },
    { id: "b", task: BRIEF, taskSlug: "79-x", costUsd: 0, attemptCostsUsd: [0], turns: [1] },
  ];
  const h = taskRunsIn(entries, BRIEF, 1, "79-x");
  assert.equal(h.runs, 2);
  assert.equal(h.attempted, 1);
});
