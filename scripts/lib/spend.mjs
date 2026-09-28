/**
 * What a run spent, in dollars, from what the stream actually reported.
 *
 * Every `claude` call the loop makes ends with a `result` event carrying `total_cost_usd`,
 * and `lib/stream-render.mjs` already hands that back as `costUsd`. This turns those
 * numbers into a ledger: one call into a charged figure, a list of calls into a task or a
 * drain total. Nothing here reads the environment, the filesystem or git — the callers own
 * the I/O, these functions own the arithmetic, which is the only part worth a test.
 *
 * The rule this exists to enforce: **an unreadable cost is unknown, never zero.** A call
 * whose result line never arrived — the process was killed, the stream was truncated, the
 * CLI changed its output — is charged at an assumed figure instead of silently costing
 * nothing. A ledger that treats "could not measure" as "$0.00" is bypassed by exactly the
 * runs most likely to be misbehaving, and every total computed from it reads lower than
 * the truth. A total therefore also carries how much of itself was assumed rather than
 * measured, so nothing downstream has to pretend an estimate is a receipt.
 */

/** Charged for one call whose cost could not be read. Overridden by AGENT_ASSUMED_USD_PER_CALL. */
export const DEFAULT_ASSUMED_USD = 1;

/** Dollars carry cents; float addition does not. Kept at six places so sums stay exact. */
const round = (n) => Math.round(n * 1e6) / 1e6;

const rateOf = (assumedUsd) =>
  typeof assumedUsd === "number" && Number.isFinite(assumedUsd) && assumedUsd >= 0
    ? assumedUsd
    : DEFAULT_ASSUMED_USD;

/**
 * Read the assumed per-call figure from a raw environment string.
 *
 * Unset, blank, unparseable or negative all fall back to the default rather than throwing:
 * a typo in `.env` must not be able to stop an unattended drain, and must not be able to
 * quietly set the assumed cost to zero either.
 */
export function parseAssumedUsd(raw, fallback = DEFAULT_ASSUMED_USD) {
  const safeFallback = rateOf(fallback);
  if (raw === undefined || raw === null || String(raw).trim() === "") return safeFallback;
  const n = Number(raw);
  return Number.isFinite(n) && n >= 0 ? n : safeFallback;
}

/**
 * One call's cost, as it should be charged.
 *
 * `costUsd` is a number from the stream's result event, or `null` when none arrived.
 * Returns `{ usd, assumed }` — `assumed` true means this figure is an estimate standing in
 * for a measurement that could not be taken.
 */
export function chargeFor(costUsd, assumedUsd = DEFAULT_ASSUMED_USD) {
  const measured =
    typeof costUsd === "number" && Number.isFinite(costUsd) && costUsd >= 0;
  return measured
    ? { usd: round(costUsd), assumed: false }
    : { usd: round(rateOf(assumedUsd)), assumed: true };
}

/** A total that has counted nothing yet. */
export const emptySpend = () => ({
  usd: 0,
  measuredUsd: 0,
  assumedUsd: 0,
  calls: 0,
  assumedCalls: 0,
});

/**
 * Sum a list of per-call costs (numbers, or `null` for unknown) into one total.
 *
 * The total reports both halves: `measuredUsd` is what the calls said they cost,
 * `assumedUsd` is what was charged for the ones that said nothing, and `assumedCalls` is
 * how many those were. A reader can therefore tell a $12 receipt from a $12 guess.
 */
export function sumSpend(costs, assumedUsd = DEFAULT_ASSUMED_USD) {
  const total = emptySpend();
  for (const cost of costs ?? []) {
    const { usd, assumed } = chargeFor(cost, assumedUsd);
    total.calls += 1;
    total.usd = round(total.usd + usd);
    if (assumed) {
      total.assumedCalls += 1;
      total.assumedUsd = round(total.assumedUsd + usd);
    } else {
      total.measuredUsd = round(total.measuredUsd + usd);
    }
  }
  return total;
}

/**
 * Add sub-totals together — a drain's tasks plus its planning, for instance.
 *
 * An input that recorded no cost at all is counted, not swallowed: `unrecorded` says how
 * many there were, and a combination of nothing *but* unrecorded totals is itself
 * unrecorded. Dropping that on the way through is how a sum of entries that knew nothing
 * would come back out as a confident `$0.00` — the one number this module exists to
 * prevent, and the last place it could still have appeared.
 */
export function combineSpend(totals) {
  const out = emptySpend();
  out.unrecorded = 0;
  let inputs = 0;
  let blank = 0;
  for (const t of totals ?? []) {
    if (!t) continue;
    inputs += 1;
    // Each input's unknown runs are counted exactly once. A total that has already been
    // combined carries its own count and must not also be counted for its `recorded: false`
    // flag; a raw one is unknown as a whole or not at all. Counting both is how a drain
    // summary — which combines totals that are themselves combinations — came to report
    // twice as many uncosted tasks as it had.
    out.unrecorded += t.unrecorded ?? (t.recorded === false ? 1 : 0);
    if (t.recorded === false) blank += 1;
    out.usd = round(out.usd + (t.usd ?? 0));
    out.measuredUsd = round(out.measuredUsd + (t.measuredUsd ?? 0));
    out.assumedUsd = round(out.assumedUsd + (t.assumedUsd ?? 0));
    out.calls += t.calls ?? 0;
    out.assumedCalls += t.assumedCalls ?? 0;
  }
  if (inputs > 0 && blank === inputs && out.calls === 0) out.recorded = false;
  return out;
}

/**
 * What one journal entry cost.
 *
 * `recorded: false` means the entry predates cost accounting entirely — it carries no cost
 * field at all. That is not the same as a call that cost nothing, and callers must render
 * it as unknown rather than as $0.00, or every run written before this existed drags the
 * reported average down.
 */
export function spendOfRun(entry, assumedUsd = DEFAULT_ASSUMED_USD) {
  if (Array.isArray(entry?.attemptCostsUsd)) {
    return { ...sumSpend(entry.attemptCostsUsd, assumedUsd), recorded: true };
  }
  if (typeof entry?.costUsd === "number" && Number.isFinite(entry.costUsd)) {
    // An entry with a total but no per-call breakdown — tolerated so a shape change later
    // does not make older entries unreadable.
    const assumedPart =
      typeof entry.costAssumedUsd === "number" && Number.isFinite(entry.costAssumedUsd)
        ? entry.costAssumedUsd
        : 0;
    const assumedCalls =
      typeof entry.costAssumedCalls === "number" ? entry.costAssumedCalls : entry.costAssumed ? 1 : 0;
    return {
      usd: round(entry.costUsd),
      // Clamped: an entry claiming more assumed than it spent is malformed, and a negative
      // measured figure would propagate into every total it joins.
      measuredUsd: Math.max(0, round(entry.costUsd - assumedPart)),
      assumedUsd: round(assumedPart),
      calls: Math.max(1, assumedCalls),
      assumedCalls,
      recorded: true,
    };
  }
  return { ...emptySpend(), recorded: false };
}

/**
 * The cost fields a journal entry carries, built from that run's per-call costs.
 *
 * One function so every writer records the same shape and `spendOfRun` has one thing to
 * read back. `attemptCostsUsd` keeps the per-call figures — a total alone cannot later be
 * told apart from a total that was half guessed.
 */
export function journalCostFields(costs, assumedUsd = DEFAULT_ASSUMED_USD) {
  const list = (costs ?? []).map((c) =>
    typeof c === "number" && Number.isFinite(c) && c >= 0 ? round(c) : null,
  );
  const total = sumSpend(list, assumedUsd);
  return {
    attemptCostsUsd: list,
    costUsd: total.usd,
    costAssumedUsd: total.assumedUsd,
    costAssumedCalls: total.assumedCalls,
    costAssumed: total.assumedCalls > 0,
  };
}

/** `$1.20`. Two places, because this is money and a reader is comparing it to an invoice. */
export function formatUsd(usd) {
  const n = typeof usd === "number" && Number.isFinite(usd) ? usd : 0;
  return `$${n.toFixed(2)}`;
}

/**
 * A total in one line, saying plainly how much of it is a guess.
 *
 * An unrecorded total reads "unknown" rather than "$0.00" — the distinction this module
 * exists for, carried all the way to what a person reads.
 */
export function describeSpend(total) {
  if (!total || total.recorded === false) return "unknown";
  const base = total.assumedCalls
    ? `${formatUsd(total.usd)} (${formatUsd(total.assumedUsd)} of it assumed, ` +
      `${total.assumedCalls} call(s) unmeasured)`
    : formatUsd(total.usd);
  // A figure that leaves runs out says so in the same breath, so the headline number is
  // never read as covering more than it counted.
  return total.unrecorded ? `${base}, plus ${total.unrecorded} with no cost recorded` : base;
}

/**
 * What one task spent, from the journal entries it wrote — of which there may be none.
 *
 * A child that exits before journalling is charged for one unmeasured call rather than for
 * nothing: it exits early only on a preflight refusal, and assuming a task that left no
 * record also left no bill is the silent zero this module exists to avoid.
 */
export function spendOfEntries(entries, assumedUsd = DEFAULT_ASSUMED_USD) {
  const list = (entries ?? []).filter(Boolean);
  if (list.length === 0) return sumSpend([null], assumedUsd);
  return combineSpend(list.map((entry) => spendOfRun(entry, assumedUsd)));
}

/**
 * The drain's closing spend block, as lines a caller prints.
 *
 * Formatting lives here rather than inline in the drain so it can be exercised without
 * spending a drain to see it, and so the assumed half of a figure can never be dropped on
 * its way to the screen. `rows` is one entry per task — `{ label, ok, spend }` — and
 * `planTotal` is what intake cost, listed separately because planning is real money that
 * belongs to no task.
 */
export function spendSummaryLines(rows, planTotal) {
  const tasks = (rows ?? []).filter(Boolean);
  // A cycle that planned nothing contributes no line and no input. Including an empty
  // planning total anyway would make every total look partly recorded, so a drain whose
  // tasks all recorded nothing would print a confident $0.00 instead of "unknown" — the
  // same silent zero, arriving through the summary instead of through a call.
  const counted = [...tasks.map((r) => r.spend), ...(planTotal?.calls ? [planTotal] : [])];
  const total = combineSpend(counted);
  const lines = ["Spend"];
  for (const row of tasks) {
    lines.push(`  ${row.ok ? "\u2713" : "\u2717"} ${String(row.label).padEnd(46)} ${describeSpend(row.spend)}`);
  }
  if (planTotal?.calls) lines.push(`  \u25c7 ${"planning".padEnd(46)} ${describeSpend(planTotal)}`);
  const across = total.recorded === false ? "" : ` across ${total.calls} call(s)`;
  lines.push(`  ${"total".padEnd(49)}${describeSpend(total)}${across}`);
  return lines;
}
