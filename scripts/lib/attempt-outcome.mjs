/**
 * How one `claude -p` attempt ended, as the runner must act on it.
 *
 *   blocked   the account refused the run — not this task's failure; stop the drain
 *   limit     stopped on a turn, budget or time limit — judge it by what it left in the tree
 *   crashed   exited non-zero for any other reason — feed that back and try again
 *   finished  exited cleanly — verify it
 *
 * A limit stop exits non-zero by design (the CLI returns 1 on `error_max_turns`, and a
 * killed process returns a signal), so reading the exit status first sent every one of them
 * down the crash path, and the work they had left was never verified. The order here is the
 * rule: refusal, then limit, then exit status.
 */
export function attemptOutcome(res) {
  if (res?.blocked) return "blocked";
  if (res?.stoppedAtLimit || res?.stoppedByCli || res?.timedOut) return "limit";
  if (res?.status !== 0) return "crashed";
  return "finished";
}
