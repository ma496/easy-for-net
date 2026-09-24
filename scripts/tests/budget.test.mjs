import test from "node:test";
import assert from "node:assert/strict";

import {
  BUDGET_EXIT_CODE,
  DEFAULT_MAX_USD_PER_DRAIN,
  DEFAULT_MAX_USD_PER_TASK,
  ceilingReached,
  chargeAgainstCeiling,
  describeCeiling,
  formatCeiling,
  parseCeiling,
} from "../lib/budget.mjs";

const ASSUMED = 2;

test("a ceiling is read from its raw setting, and a blank one falls back to the default", () => {
  assert.equal(parseCeiling("12.5", 50), 12.5);
  assert.equal(parseCeiling(undefined, 50), 50);
  assert.equal(parseCeiling(null, 50), 50);
  assert.equal(parseCeiling("   ", 50), 50);
});

test("a typo falls back to the default ceiling, never to no ceiling", () => {
  // The failure mode this guards: a bad value that silently removes the one thing standing
  // between a spin loop and a month's credit. Falling back to the default keeps a ceiling.
  for (const bad of ["abc", "$20", "-5", "NaN", {}, []]) {
    assert.equal(parseCeiling(bad, 50), 50, `should fall back for ${JSON.stringify(bad)}`);
  }
});

test("an explicit zero is honoured as a deliberate 'spend nothing'", () => {
  // A drain set to 0 refuses before planning and before its first task, so it truly spends
  // nothing — which is what makes the drain ceiling demonstrable without spending anything.
  // A task set to 0 is different: its check happens after an attempt, so it pays for one.
  assert.equal(parseCeiling("0", 50), 0);
  assert.equal(ceilingReached(0, 0), true);
});

test("a ceiling can be turned off in words rather than by typing a large number", () => {
  for (const off of ["off", "none", "unlimited", "INFINITY", " Infinity "]) {
    assert.equal(parseCeiling(off, 50), Infinity, `${off} should mean no ceiling`);
  }
  assert.equal(ceilingReached(1e9, Infinity), false);
});

test("only the documented spellings turn a ceiling off", () => {
  // Every unrecognised value falls back to the default, so a spelling the code accepts and
  // the documentation does not is the one input class that removes a ceiling silently.
  for (const near of ["no", "inf", "nil", "disabled", "unlimited!"]) {
    assert.equal(parseCeiling(near, 50), 50, `${near} is not a documented "off" spelling`);
  }
});

test("a ceiling is reached at it, not only past it", () => {
  assert.equal(ceilingReached(9.99, 10), false);
  assert.equal(ceilingReached(10, 10), true);
  assert.equal(ceilingReached(10.01, 10), true);
});

test("a measured call is charged exactly what it reported, and the total accumulates", () => {
  const first = chargeAgainstCeiling({ spentUsd: 0, costUsd: 1.25, ceilingUsd: 10, assumedUsd: ASSUMED });
  assert.equal(first.spentUsd, 1.25);
  assert.equal(first.chargedUsd, 1.25);
  assert.equal(first.assumed, false);
  assert.equal(first.exhausted, false);
  assert.equal(first.remainingUsd, 8.75);

  const second = chargeAgainstCeiling({
    spentUsd: first.spentUsd,
    costUsd: 0.75,
    ceilingUsd: 10,
    assumedUsd: ASSUMED,
  });
  assert.equal(second.spentUsd, 2);
});

test("a call whose cost could not be read is charged at the assumed figure, not at zero", () => {
  // The rule the whole ceiling rests on. If a `result` line that cannot be parsed counted
  // as free, a run whose stream is broken — exactly the run most likely to be misbehaving —
  // could loop forever under any ceiling.
  for (const unreadable of [null, undefined, NaN, "0.40", {}, -1]) {
    const charged = chargeAgainstCeiling({
      spentUsd: 0,
      costUsd: unreadable,
      ceilingUsd: 10,
      assumedUsd: ASSUMED,
    });
    assert.equal(charged.chargedUsd, ASSUMED, `should charge the assumed rate for ${String(unreadable)}`);
    assert.equal(charged.assumed, true);
    assert.notEqual(charged.spentUsd, 0);
  }
});

test("unreadable costs alone are enough to exhaust a ceiling", () => {
  // Three attempts that all failed to report a cost must still stop the fourth.
  let spent = 0;
  let exhausted = false;
  for (let i = 0; i < 3; i += 1) {
    const charged = chargeAgainstCeiling({ spentUsd: spent, costUsd: null, ceilingUsd: 5, assumedUsd: ASSUMED });
    spent = charged.spentUsd;
    exhausted = charged.exhausted;
  }
  assert.equal(spent, 6);
  assert.equal(exhausted, true);
});

test("exhaustion describes the next call, never the one already paid for", () => {
  // A ceiling enforced by throwing away work already charged spends the money twice. The
  // call that crossed the line still completes; what stops is whatever would come after it.
  const charged = chargeAgainstCeiling({ spentUsd: 4, costUsd: 9, ceilingUsd: 10, assumedUsd: ASSUMED });
  assert.equal(charged.spentUsd, 13);
  assert.equal(charged.chargedUsd, 9);
  assert.equal(charged.exhausted, true);
  assert.equal(charged.remainingUsd, 0);
});

test("no ceiling means nothing is ever exhausted", () => {
  const charged = chargeAgainstCeiling({ spentUsd: 900, costUsd: 100, ceilingUsd: Infinity });
  assert.equal(charged.exhausted, false);
  assert.equal(charged.remainingUsd, Infinity);
});

test("repeated small charges do not drift off the ceiling by float error", () => {
  let spent = 0;
  for (let i = 0; i < 10; i += 1) {
    spent = chargeAgainstCeiling({ spentUsd: spent, costUsd: 0.1, ceilingUsd: 1 }).spentUsd;
  }
  assert.equal(spent, 1);
  assert.equal(ceilingReached(spent, 1), true);
});

test("a stop names the setting and both figures, so a reader knows which knob to turn", () => {
  assert.equal(
    describeCeiling("AGENT_MAX_USD_PER_DRAIN", 12.5, 10),
    "spent $12.50 against AGENT_MAX_USD_PER_DRAIN=$10.00",
  );
  assert.equal(formatCeiling(Infinity), "no ceiling");
});

test("the defaults are generous enough to clear the drains this repo has actually run", () => {
  // Worst on record when this was written: four attempts on one task, and twelve task runs
  // on the busiest day. Charged at the assumed $1 per call that is $4 and $12 — so the
  // defaults must sit well above both, or the first person a ceiling annoys sets it to
  // infinity and the loop ends up with no ceiling at all.
  assert.ok(DEFAULT_MAX_USD_PER_TASK >= 4 * 5);
  assert.ok(DEFAULT_MAX_USD_PER_DRAIN >= DEFAULT_MAX_USD_PER_TASK);
  assert.equal(parseCeiling(undefined, DEFAULT_MAX_USD_PER_TASK), DEFAULT_MAX_USD_PER_TASK);
});

test("a budget stop has its own exit status, distinct from a verification failure", () => {
  assert.notEqual(BUDGET_EXIT_CODE, 0);
  assert.notEqual(BUDGET_EXIT_CODE, 1);
});
