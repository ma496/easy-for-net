import { test } from "node:test";
import assert from "node:assert/strict";
import { classifyDoneShip, commitShaFromRecord } from "../lib/shipped-on-main.mjs";

test("a brief whose record and code are committed is clean", () => {
  assert.equal(
    classifyDoneShip({
      hasRecord: true,
      recordOnMain: true,
      commitSha: "abc1234",
      commitOnMain: true,
    }),
    "clean",
  );
});

test("a brief whose build record is untracked is not_on_main", () => {
  assert.equal(
    classifyDoneShip({
      hasRecord: true,
      recordOnMain: false,
      commitSha: null,
      commitOnMain: false,
    }),
    "not_on_main",
  );
  // Even a forged SHA in an untracked record does not count — the record is not on main.
  assert.equal(
    classifyDoneShip({
      hasRecord: true,
      recordOnMain: false,
      commitSha: "deadbeef",
      commitOnMain: true,
    }),
    "not_on_main",
  );
});

test("a brief with no record at all stays unrecorded, not the new class", () => {
  assert.equal(
    classifyDoneShip({
      hasRecord: false,
      recordOnMain: false,
      commitSha: null,
      commitOnMain: false,
    }),
    "unrecorded",
  );
});

test("*(uncommitted)* and **none** parse as no SHA", () => {
  assert.equal(commitShaFromRecord("| **Commit** | *(uncommitted)* |"), null);
  assert.equal(commitShaFromRecord("| **Commit** | **none** |"), null);
  assert.equal(commitShaFromRecord("| **Commit** | `61e7e01` |"), "61e7e01");
  assert.equal(commitShaFromRecord("| **Commit** | `abcdef0123456789` |"), "abcdef0123456789");
});

test("a tracked record with no real commit on main is not_on_main", () => {
  assert.equal(
    classifyDoneShip({
      hasRecord: true,
      recordOnMain: true,
      commitSha: null,
      commitOnMain: false,
    }),
    "not_on_main",
  );
  assert.equal(
    classifyDoneShip({
      hasRecord: true,
      recordOnMain: true,
      commitSha: "abc1234",
      commitOnMain: false,
    }),
    "not_on_main",
  );
});
