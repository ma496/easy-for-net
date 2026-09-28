/**
 * The ceilings the loop refuses to spend past.
 *
 * `CLAUDE.md` has always said that anything spending unbounded OpenAI credit is outside the
 * loop by design, but that was a convention and this repo's own lesson is that conventions
 * do not hold when nobody is watching. The timer is live: an over-large spec, a task
 * burning its attempts against a flaky check, or a planner that emits twenty tasks instead
 * of six all spend real money overnight. `lib/spend.mjs` made that visible; this makes it
 * bounded.
 *
 * Two ceilings, both **refusing rather than warning**:
 *
 *   AGENT_MAX_USD_PER_TASK    one task's total across all of its attempts
 *   AGENT_MAX_USD_PER_DRAIN   a whole drain, planning included
 *
 * Everything here is arithmetic over numbers the callers already hold: no environment
 * reads, no filesystem, no git. The callers own the I/O and the decision to stop; this owns
 * the question "may another call be made", which is the only part worth a test.
 *
 * Two rules the arithmetic exists to keep:
 *
 *   - **An unreadable cost is charged, not skipped.** A call whose result line never
 *     arrived goes through `chargeFor` at the assumed figure, so a parse failure counts
 *     against a ceiling instead of slipping past it. A ledger that treats "could not
 *     measure" as free is bypassed by exactly the runs most likely to be misbehaving.
 *   - **The check is against what has already been spent, never against a forecast.** No
 *     function here predicts what the next call will cost. A ceiling that guessed would
 *     refuse work that would have been affordable, and this repo's own argument applies:
 *     a rule that trips on ordinary work is a rule somebody sets to infinity.
 */
import { chargeFor } from "./spend.mjs";

/**
 * What one task may spend across every attempt, and what a whole drain may spend.
 *
 * Chosen to clear every drain this repository has actually run by an order of magnitude —
 * a ceiling that trips on ordinary work gets raised to infinity by the first person it
 * annoys, which leaves the loop worse off than having no ceiling at all.
 *
 * These are estimates, not measurements, and say so. The journal only began carrying costs
 * with task 19, so of its 26 entries exactly two name a figure. The anchors are therefore
 * attempt counts charged at the assumed per-call rate: the worst single task on record took
 * four attempts, and the busiest day on record ran twelve task runs — how many drains those
 * twelve were split across is not recoverable from the journal, so they are treated as one.
 * That is roughly $4 and $12 at the assumed rate, and these ceilings sit an order of
 * magnitude above both, which is the headroom a real call costing several times the assumed
 * rate needs.
 */
export const DEFAULT_MAX_USD_PER_TASK = 50;
export const DEFAULT_MAX_USD_PER_DRAIN = 200;

/**
 * Exit status meaning "stopped because of a ceiling", as opposed to 1 for "the work did not
 * verify". The two call for opposite responses from whoever reads it — one needs a bigger
 * budget or a smaller task, the other needs the code fixed — and the parent processes
 * already branch on the child's exit code, so that is the channel rather than a second one.
 */
export const BUDGET_EXIT_CODE = 3;

/** Ceilings are money: kept at six places so repeated addition stays exact. */
const round = (n) => Math.round(n * 1e6) / 1e6;

/**
 * Spellings that mean "no ceiling at all", for someone who deliberately wants none.
 *
 * Deliberately the exact set `.env.example` documents, and no wider. Every other
 * unrecognised value falls back to the default ceiling, so an accepted-but-undocumented
 * spelling would be the one input class that silently removes a ceiling — which is the
 * failure this module exists to prevent.
 */
const OFF = new Set(["off", "none", "unlimited", "infinity"]);

/**
 * Read a ceiling from a raw environment string.
 *
 * Unset or blank means the default. `off`, `none`, `unlimited` and `infinity` mean exactly
 * that — `Infinity` — because someone who genuinely wants no ceiling should be able to say
 * so plainly instead of typing a large number and hoping.
 *
 * A typo falls back to the default rather than to no ceiling, and never throws: a bad value
 * must not be able to stop an unattended drain, and must not be able to quietly remove the
 * one thing standing between a spin loop and a month's credit.
 *
 * An explicit `0` is honoured rather than ignored, and means the lowest each ceiling can
 * enforce: a drain set to 0 starts no task and plans no spec, so it truly spends nothing,
 * while a task set to 0 still pays for the one attempt it takes to notice — the per-task
 * check happens after an attempt, because nothing knows what an attempt costs until it has
 * been made. That is also what makes the drain ceiling demonstrable without spending
 * anything to demonstrate it.
 */
export function parseCeiling(raw, fallback) {
  const safeFallback =
    typeof fallback === "number" && !Number.isNaN(fallback) && fallback >= 0 ? fallback : Infinity;
  if (raw === undefined || raw === null || String(raw).trim() === "") return safeFallback;
  const text = String(raw).trim().toLowerCase();
  if (OFF.has(text)) return Infinity;
  const n = Number(text);
  return Number.isFinite(n) && n >= 0 ? n : safeFallback;
}

/** Whether spending so far has reached or passed the ceiling. `Infinity` never has. */
export function ceilingReached(spentUsd, ceilingUsd) {
  if (!Number.isFinite(ceilingUsd)) return false;
  const spent = typeof spentUsd === "number" && Number.isFinite(spentUsd) ? spentUsd : 0;
  return spent >= ceilingUsd;
}

/**
 * Charge one call against a ceiling and say whether another may follow.
 *
 * `costUsd` is what that call reported — a number, or `null`/`undefined` when its result
 * line could not be read, in which case it is charged at `assumedUsd` and counted like any
 * other spend. Returns the new running total, what this call was charged, and `exhausted`:
 * true when the total has reached the ceiling and nothing further may start.
 *
 * `exhausted` describes the *next* call, not this one. The call just charged has already
 * happened and is never retracted — enforcing a ceiling by throwing away work already paid
 * for spends the money twice.
 */
export function chargeAgainstCeiling({ spentUsd = 0, costUsd, ceilingUsd = Infinity, assumedUsd } = {}) {
  const before = typeof spentUsd === "number" && Number.isFinite(spentUsd) && spentUsd >= 0 ? spentUsd : 0;
  const charged = chargeFor(costUsd, assumedUsd);
  const after = round(before + charged.usd);
  return {
    spentUsd: after,
    chargedUsd: charged.usd,
    assumed: charged.assumed,
    exhausted: ceilingReached(after, ceilingUsd),
    remainingUsd: Number.isFinite(ceilingUsd) ? round(Math.max(0, ceilingUsd - after)) : Infinity,
  };
}

/** `$50.00`, or `no ceiling` — a ceiling is compared against an invoice, so two places. */
export function formatCeiling(ceilingUsd) {
  return Number.isFinite(ceilingUsd) ? `$${ceilingUsd.toFixed(2)}` : "no ceiling";
}

/**
 * One line saying what was spent, against which ceiling, naming the setting.
 *
 * The setting's name is in the message on purpose: whoever reads a stopped drain at 3am
 * needs to know which knob to turn, and a message that says only "budget exhausted" sends
 * them to the source.
 */
export function describeCeiling(setting, spentUsd, ceilingUsd) {
  const spent = typeof spentUsd === "number" && Number.isFinite(spentUsd) ? spentUsd : 0;
  return `spent $${spent.toFixed(2)} against ${setting}=${formatCeiling(ceilingUsd)}`;
}
