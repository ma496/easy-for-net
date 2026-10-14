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

test("a session closed after it settled on a successful result is finished, not crashed", () => {
  // Killed by the runner, so it exits non-zero — but its work was done and must be verified.
  assert.equal(attemptOutcome({ status: 1, settled: true, isError: false, resultSubtype: "success" }), "finished");
  assert.equal(attemptOutcome({ status: null, settled: true, isError: false, resultSubtype: "success" }), "finished");
});

test("a session that settled on an error result is the crash it reported", () => {
  assert.equal(attemptOutcome({ status: 1, settled: true, isError: true, resultSubtype: "error_during_execution" }), "crashed");
  assert.equal(attemptOutcome({ status: 1, settled: true, isError: false, resultSubtype: null }), "crashed");
});

test("a limit outranks a settle, and a refusal outranks both", () => {
  assert.equal(attemptOutcome({ status: 1, settled: true, timedOut: true, resultSubtype: "success" }), "limit");
  assert.equal(attemptOutcome({ status: 1, settled: true, resultSubtype: "error_max_turns", stoppedByCli: true }), "limit");
  assert.equal(attemptOutcome({ status: 1, settled: true, resultSubtype: "success", blocked: "usage limit" }), "blocked");
});

test("any other non-zero exit is a crash, and a clean one is finished", () => {
  assert.equal(attemptOutcome({ status: 2 }), "crashed");
  assert.equal(attemptOutcome({ status: 0 }), "finished");
  assert.equal(attemptOutcome(undefined), "crashed");
});
