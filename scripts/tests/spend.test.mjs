import test from "node:test";
import assert from "node:assert/strict";

import {
  chargeFor,
  emptySpend,
  combineSpend,
  describeSpend,
  DEFAULT_ASSUMED_USD,
  formatUsd,
  journalCostFields,
  parseAssumedUsd,
  spendOfEntries,
  spendOfRun,
  spendSummaryLines,
  sumSpend,
} from "../lib/spend.mjs";

const ASSUMED = 2;

test("a measured cost is charged exactly as it was reported", () => {
  const charged = chargeFor(1.37, ASSUMED);
  assert.equal(charged.usd, 1.37);
  assert.equal(charged.assumed, false);
});

test("a cost that could not be read is charged at the assumed figure, never at zero", () => {
  // The whole point of the module: a call whose result line never arrived is unknown, not
  // free. Silently charging zero is how a ceiling gets bypassed by the runs most likely to
  // be misbehaving.
  for (const unreadable of [null, undefined, NaN, "0.40", {}, -1]) {
    const charged = chargeFor(unreadable, ASSUMED);
    assert.equal(charged.usd, ASSUMED, `should charge the assumed rate for ${String(unreadable)}`);
    assert.equal(charged.assumed, true);
    assert.notEqual(charged.usd, 0);
  }
});

test("a measured zero is still a measurement", () => {
  // A call that genuinely cost nothing reported a number; it is not an unknown.
  const charged = chargeFor(0, ASSUMED);
  assert.equal(charged.usd, 0);
  assert.equal(charged.assumed, false);
});

test("a total adds its calls up and says how many were assumed", () => {
  const total = sumSpend([1.25, null, 0.5], ASSUMED);
  assert.equal(total.calls, 3);
  assert.equal(total.assumedCalls, 1);
  assert.equal(total.usd, 3.75); // 1.25 + 2 (assumed) + 0.5
  assert.equal(total.measuredUsd, 1.75);
  assert.equal(total.assumedUsd, 2);
});

test("a total of measured calls reports nothing as assumed", () => {
  const total = sumSpend([0.1, 0.2, 0.3], ASSUMED);
  assert.equal(total.assumedCalls, 0);
  assert.equal(total.assumedUsd, 0);
  assert.equal(total.usd, 0.6); // not 0.6000000000000001
});

test("an empty list totals zero without claiming anything was assumed", () => {
  const total = sumSpend([], ASSUMED);
  assert.deepEqual(total, { usd: 0, measuredUsd: 0, assumedUsd: 0, calls: 0, assumedCalls: 0 });
});

test("sub-totals combine into a drain total", () => {
  const tasks = sumSpend([1.5, null], ASSUMED);
  const planning = sumSpend([0.25], ASSUMED);
  const drain = combineSpend([tasks, planning]);
  assert.equal(drain.usd, 3.75);
  assert.equal(drain.calls, 3);
  assert.equal(drain.assumedCalls, 1);
  assert.equal(drain.assumedUsd, 2);
  assert.equal(drain.measuredUsd, 1.75);
});

test("combining ignores gaps rather than throwing", () => {
  const drain = combineSpend([null, undefined, sumSpend([1], ASSUMED)]);
  assert.equal(drain.usd, 1);
  assert.equal(drain.calls, 1);
});

test("the assumed rate falls back rather than becoming zero on a bad setting", () => {
  assert.equal(parseAssumedUsd("0.75"), 0.75);
  assert.equal(parseAssumedUsd("0"), 0); // an explicit zero is a choice, not a typo
  for (const bad of [undefined, null, "", "   ", "abc", "-4"]) {
    assert.equal(parseAssumedUsd(bad), DEFAULT_ASSUMED_USD, `should fall back for ${JSON.stringify(bad)}`);
  }
  assert.equal(parseAssumedUsd(undefined, 3), 3);
});

test("journal fields keep the per-call figures alongside the total", () => {
  const fields = journalCostFields([0.4, null], ASSUMED);
  assert.deepEqual(fields.attemptCostsUsd, [0.4, null]);
  assert.equal(fields.costUsd, 2.4);
  assert.equal(fields.costAssumedUsd, 2);
  assert.equal(fields.costAssumedCalls, 1);
  assert.equal(fields.costAssumed, true);
});

test("a run's spend is read back out of the entry it wrote", () => {
  const entry = { id: "run-1", ...journalCostFields([1, 2], ASSUMED) };
  const spend = spendOfRun(entry, ASSUMED);
  assert.equal(spend.recorded, true);
  assert.equal(spend.usd, 3);
  assert.equal(spend.assumedCalls, 0);
});

test("a run recorded before cost accounting existed is unknown, not zero", () => {
  // Every entry already in .agent-runs/journal.jsonl looks like this. Rendering it as
  // $0.00 would report the loop as free for its whole history.
  const spend = spendOfRun({ id: "run-old", outcome: "verified", attempts: 1 }, ASSUMED);
  assert.equal(spend.recorded, false);
  assert.equal(describeSpend(spend), "unknown");
});

test("an entry carrying only a total is still readable", () => {
  const spend = spendOfRun({ costUsd: 5, costAssumedUsd: 2, costAssumedCalls: 1 }, ASSUMED);
  assert.equal(spend.recorded, true);
  assert.equal(spend.usd, 5);
  assert.equal(spend.measuredUsd, 3);
  assert.equal(spend.assumedCalls, 1);
});

test("a described total says plainly when part of it was guessed", () => {
  assert.equal(describeSpend(sumSpend([1.5], ASSUMED)), "$1.50");
  assert.match(describeSpend(sumSpend([1.5, null], ASSUMED)), /\$3\.50 \(\$2\.00 of it assumed, 1 call\(s\) unmeasured\)/);
  assert.equal(formatUsd(0), "$0.00");
  assert.equal(formatUsd(undefined), "$0.00");
});

test("the drain summary lists every task, planning, and a total", () => {
  const lines = spendSummaryLines(
    [
      { label: "record-loop-spend", ok: true, spend: sumSpend([1.5], ASSUMED) },
      { label: "some-failed-task", ok: false, spend: sumSpend([null], ASSUMED) },
    ],
    sumSpend([0.25], ASSUMED),
  );
  const block = lines.join("\n");
  // A failed task spent money too, and is listed for that reason.
  assert.match(block, /✓ record-loop-spend\s+\$1\.50/);
  assert.match(block, /✗ some-failed-task\s+\$2\.00 \(\$2\.00 of it assumed/);
  assert.match(block, /planning\s+\$0\.25/);
  assert.match(block, /total\s+\$3\.75 \(\$2\.00 of it assumed, 1 call\(s\) unmeasured\) across 3 call\(s\)/);
});

test("the summary omits a planning line when nothing was planned", () => {
  const lines = spendSummaryLines([{ label: "solo", ok: true, spend: sumSpend([2], ASSUMED) }], emptySpend());
  assert.equal(lines.filter((l) => l.includes("planning")).length, 0);
  assert.match(lines.at(-1), /total\s+\$2\.00 across 1 call\(s\)/);
});

test("combining totals that recorded nothing does not produce a confident $0.00", () => {
  // The last place a silent zero could still appear: a sum of entries that all knew
  // nothing used to come back out as a measured zero.
  const nothing = combineSpend([spendOfRun({ id: "old" }, ASSUMED), spendOfRun({ id: "older" }, ASSUMED)]);
  assert.equal(nothing.recorded, false);
  assert.equal(nothing.unrecorded, 2);
  assert.equal(describeSpend(nothing), "unknown");
});

test("a partly-recorded total names what it left out instead of hiding it", () => {
  const mixed = combineSpend([
    spendOfRun({ ...journalCostFields([1.5], ASSUMED) }, ASSUMED),
    spendOfRun({ id: "old" }, ASSUMED),
  ]);
  assert.equal(mixed.usd, 1.5);
  assert.equal(mixed.unrecorded, 1);
  // Something was counted, so the figure stands and says what it left out.
  assert.equal(describeSpend(mixed), "$1.50, plus 1 with no cost recorded");
});

test("a task that journalled nothing at all is charged, not zeroed", () => {
  // The drain's per-task read. A child exits before journalling only on a preflight
  // refusal, and assuming it also left no bill is the silent zero this module prevents.
  const spend = spendOfEntries([], ASSUMED);
  assert.equal(spend.usd, ASSUMED);
  assert.equal(spend.assumedCalls, 1);
});

test("a task's spend is the sum of the entries it journalled", () => {
  const spend = spendOfEntries(
    [{ ...journalCostFields([0.4, null], ASSUMED) }, { ...journalCostFields([0.1], ASSUMED) }],
    ASSUMED,
  );
  assert.equal(spend.usd, 2.5); // 0.4 + 2 (assumed) + 0.1
  assert.equal(spend.calls, 3);
  assert.equal(spend.assumedCalls, 1);
});

test("a task whose entry carries no cost fields reads as unknown, not as free", () => {
  const spend = spendOfEntries([{ id: "run-old", outcome: "verified" }], ASSUMED);
  assert.equal(describeSpend(spend), "unknown");
  assert.notEqual(describeSpend(spend), "$0.00");
});

test("combining an already-combined total counts its unknowns once, not twice", () => {
  // The drain's shape: spendOfEntries returns a combined total, and the summary combines
  // those again. Counting an input's carried `unrecorded` *and* its own `recorded: false`
  // flag made the closing line claim twice as many uncosted tasks as there were.
  const task = spendOfEntries([{ id: "run-old" }], ASSUMED);
  assert.equal(task.unrecorded, 1);
  const once = combineSpend([task]);
  assert.equal(once.unrecorded, 1);
  assert.equal(combineSpend([once]).unrecorded, 1); // idempotent however deeply it nests
  assert.equal(
    describeSpend(combineSpend([sumSpend([1.5], ASSUMED), task])),
    "$1.50, plus 1 with no cost recorded",
  );
});

test("a drain that measured nothing at all says unknown, not $0.00", () => {
  // An empty planning total must not count as a recorded input; if it did, every total
  // would look partly measured and this line would read $0.00.
  const nothing = spendOfEntries([{ id: "run-old" }], ASSUMED);
  const lines = spendSummaryLines(
    [
      { label: "one", ok: false, spend: nothing },
      { label: "two", ok: false, spend: nothing },
    ],
    emptySpend(),
  );
  assert.match(lines.at(-1), /^\s+total\s+unknown$/);
  assert.doesNotMatch(lines.at(-1), /\$0\.00/);
});
