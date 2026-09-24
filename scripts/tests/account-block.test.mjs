import { test } from "node:test";
import assert from "node:assert/strict";
import { parseResetAt, minutesUntil } from "../lib/account-block.mjs";

const at = (h, m = 0) => {
  const d = new Date(2026, 8, 8, h, m, 0, 0);
  return d;
};

test("a reset later today is today", () => {
  const now = at(20, 0);
  const r = parseResetAt("You've hit your session limit · resets 11:30pm (Asia/Karachi)", now);
  assert.equal(r.getHours(), 23);
  assert.equal(r.getMinutes(), 30);
  assert.equal(r.getDate(), now.getDate());
});

test("a reset already past today means tomorrow", () => {
  // The real case: told at 8pm that the limit resets at 1:10am.
  const now = at(20, 0);
  const r = parseResetAt("resets 1:10am (Asia/Karachi)", now);
  assert.equal(r.getHours(), 1);
  assert.equal(r.getMinutes(), 10);
  assert.equal(r.getDate(), now.getDate() + 1);
});

test("noon and midnight are not swapped", () => {
  assert.equal(parseResetAt("resets 12:00am", at(13)).getHours(), 0);
  assert.equal(parseResetAt("resets 12:30pm", at(1)).getHours(), 12);
});

test("a reset at exactly now has already happened", () => {
  const now = at(6, 10);
  assert.equal(parseResetAt("resets 6:10am", now).getDate(), now.getDate() + 1);
});

test("wording that names no time backs off not at all", () => {
  // Must degrade to the old behaviour — stop, try next tick — never to a guessed wait.
  assert.equal(parseResetAt("You've hit your session limit", at(9)), null);
  assert.equal(parseResetAt("", at(9)), null);
  assert.equal(parseResetAt(null, at(9)), null);
});

test("a nonsense clock is refused rather than believed", () => {
  assert.equal(parseResetAt("resets 99:99am", at(9)), null);
  assert.equal(parseResetAt("resets 7:75am", at(9)), null);
});

test("minutesUntil never reports zero", () => {
  assert.equal(minutesUntil(at(9, 0), at(9, 0)), 1);
  assert.equal(minutesUntil(at(9, 30), at(9, 0)), 30);
});
