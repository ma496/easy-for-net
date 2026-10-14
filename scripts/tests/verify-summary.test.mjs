import { strict as assert } from "node:assert";
import { test } from "node:test";
import { verifySummary } from "../run-journal.mjs";

test("a run with no recorded verifies has no summary", () => {
  assert.equal(verifySummary({}), null);
  assert.equal(verifySummary({ verifies: [] }), null);
});

test("time, count, reuse and who asked are all reported", () => {
  const run = {
    verifies: [
      { by: "session", seconds: 200, status: 0, reused: false },
      { by: "runner", seconds: 0, status: 0, reused: true },
    ],
  };
  assert.equal(verifySummary(run), "verify 3m 20s over 2 run(s), 1 reused, 1 asked by the session");
});

test("under a minute reads in seconds, with nothing to add", () => {
  assert.equal(verifySummary({ verifies: [{ by: "runner", seconds: 42, status: 1, reused: false }] }), "verify 42s over 1 run(s)");
});
