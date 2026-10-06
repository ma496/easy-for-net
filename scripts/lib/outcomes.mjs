/**
 * The outcomes a journal entry can record, and what each one means to a reader.
 *
 * Writers and readers used to agree on these only by coincidence. `agent-run.mjs` records a
 * landed task as `committed`; the history reader treated anything that was not `verified` or
 * `shipped` as a failure, so every landed run of a similar task was fed into the next brief
 * as "attempted before and did not reach a verified state" — and the status table printed
 * `?` for it. One table, read by both ends.
 */
export const OUTCOMES = {
  committed: { mark: "✓", landed: true },
  verified: { mark: "✓", landed: true },
  shipped: { mark: "⇪", landed: true },
  planned: { mark: "◇" },
  // A planning call that ran but whose output was refused: misnamed tasks, or edits outside the queue.
  rejected: { mark: "◆" },
  failed: { mark: "✗", unfinished: true },
  abandoned: { mark: "–", unfinished: true },
  // Out of money is not out of ideas: nothing judged the approach.
  budget: { mark: "○", unfinished: true },
  // The account or the network refused the run; the task itself was not at fault.
  blocked: { mark: "⊘", unfinished: true },
};

/** Journal entries that record something other than an attempt at a task. */
export const isBookkeeping = (entry) => Boolean(entry?.kind);

export const markOf = (outcome) => OUTCOMES[outcome]?.mark ?? "?";
export const isLanded = (outcome) => Boolean(OUTCOMES[outcome]?.landed);

/** A run that ended without its work landing — the history a retry should hear about. */
export const isUnfinished = (entry) => !isBookkeeping(entry) && Boolean(OUTCOMES[entry?.outcome]?.unfinished);

/** Count task attempts by what became of them, leaving bookkeeping entries out. */
export function tally(entries) {
  const out = { landed: 0, failed: 0, budget: 0, blocked: 0, other: 0 };
  for (const e of entries ?? []) {
    if (isBookkeeping(e)) continue;
    if (isLanded(e.outcome)) out.landed += 1;
    else if (e.outcome === "budget") out.budget += 1;
    else if (e.outcome === "blocked") out.blocked += 1;
    else if (e.outcome === "failed" || e.outcome === "abandoned") out.failed += 1;
    else out.other += 1;
  }
  return out;
}
