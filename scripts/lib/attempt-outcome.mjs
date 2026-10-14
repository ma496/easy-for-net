/**
 * How one `claude -p` attempt ended, as the runner must act on it.
 *
 *   blocked   the account refused the run — not this task's failure; stop the drain
 *   limit     stopped on a turn, budget or time limit — judge it by what it left in the tree
 *   crashed   exited non-zero for any other reason — feed that back and try again
 *   finished  exited cleanly, or settled on a successful result — verify it
 *
 * A limit stop exits non-zero by design (the CLI returns 1 on `error_max_turns`, and a
 * killed process returns a signal), so reading the exit status first sent every one of them
 * down the crash path, and the work they had left was never verified. The order here is the
 * rule: refusal, then limit, then a settled session, then exit status.
 *
 * A settled session (lib/session-settle.mjs) finished its work and then did not exit, so the
 * runner closed it — non-zero, being killed. It is finished when its last result says it
 * succeeded; one that settled on an error result is the crash it reported.
 */
export function attemptOutcome(res) {
  if (res?.blocked) return "blocked";
  if (res?.stoppedAtLimit || res?.stoppedByCli || res?.timedOut) return "limit";
  if (res?.settled && !res.isError && res.resultSubtype === "success") return "finished";
  if (res?.status !== 0) return "crashed";
  return "finished";
}
