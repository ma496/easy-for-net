import { strict as assert } from "node:assert";
import { test } from "node:test";
import { isLanded, isUnfinished, markOf, tally } from "../lib/outcomes.mjs";

test("committed — what the runner writes when a task lands — is landed, not a failure", () => {
  assert.equal(isLanded("committed"), true);
  assert.equal(isUnfinished({ outcome: "committed" }), false);
  assert.notEqual(markOf("committed"), "?");
});

test("failed, abandoned, budget and blocked are unfinished history for a retry", () => {
  for (const outcome of ["failed", "abandoned", "budget", "blocked"]) {
    assert.equal(isUnfinished({ outcome }), true, outcome);
  }
});

test("bookkeeping entries are never history, whatever outcome they carry", () => {
  assert.equal(isUnfinished({ kind: "plan", outcome: "failed" }), false);
  assert.equal(isUnfinished({ kind: "budget-reset" }), false);
});

test("the tally counts attempts by fate and leaves bookkeeping out", () => {
  const t = tally([
    { outcome: "committed" },
    { outcome: "verified" },
    { outcome: "failed" },
    { outcome: "budget" },
    { outcome: "blocked" },
    { kind: "plan", outcome: "planned" },
    { kind: "budget-reset" },
  ]);
  assert.deepEqual(t, { landed: 2, failed: 1, budget: 1, blocked: 1, other: 0 });
});
