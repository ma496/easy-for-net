import { strict as assert } from "node:assert";
import { test } from "node:test";
import { attemptOutcome } from "../lib/attempt-outcome.mjs";

test("a refusal is blocked, whatever else the attempt reports", () => {
  assert.equal(attemptOutcome({ blocked: "usage limit", status: 1, stoppedByCli: true }), "blocked");
});

test("a limit stop is judged by its work even though it exits non-zero", () => {
  // The CLI exits 1 on error_max_turns; a killed process exits on a signal. Reading the
  // status first sent both down the crash path and their work was never verified.
  assert.equal(attemptOutcome({ status: 1, stoppedByCli: true }), "limit");
  assert.equal(attemptOutcome({ status: 1, stoppedAtLimit: true }), "limit");
  assert.equal(attemptOutcome({ status: 1, timedOut: true }), "limit");
});

test("any other non-zero exit is a crash, and a clean one is finished", () => {
  assert.equal(attemptOutcome({ status: 2 }), "crashed");
  assert.equal(attemptOutcome({ status: 0 }), "finished");
  assert.equal(attemptOutcome(undefined), "crashed");
});
