/**
 * Whether a `claude -p` session has finished even though its process has not exited.
 *
 * The CLI has been seen to write its final `result`, announce that no background task is left,
 * and then simply not exit — and the runner, waiting on the process, sat idle until the
 * attempt's two-hour wall clock killed it and filed a finished, reviewed task as "stopped at
 * the time limit". This reads the stream for the end instead.
 *
 * The first `result` is not the end. A session that dispatched reviewers in the background
 * ends its turn while they run, and is woken by each one's report; a CLI may write a `result`
 * for every one of those turns. So a session is settled only when all three hold:
 *
 *   - a `result` came after the parent's last assistant turn (it is not mid-turn),
 *   - nothing is pending in the background, and
 *   - the stream has then been quiet for `quietMs` — a late notification starts a new turn,
 *     and its events reset the clock.
 *
 * What is pending is read from `background_tasks_changed`, which carries the whole list each
 * time. A CLI that never sends it is followed through the task lifecycle instead: a task the
 * session started is pending until its notification or a terminal status. Tasks a subagent
 * started are its own business — the subagent itself is the session's task.
 *
 * Pure: time is passed in, and nothing here touches a process.
 */
import { backgroundTaskOf, backgroundTasksOf, isSubagentEvent, normalizeEvent } from "./claude-events.mjs";

/** `task_updated` statuses after which a task is no longer running. */
const TERMINAL_STATUSES = new Set(["completed", "failed", "killed", "stopped", "cancelled", "canceled", "error"]);

/**
 * @param {{ quietMs: number }} options
 * @returns {{ observe(raw: object, nowMs: number): void, isSettled(nowMs: number): boolean,
 *   state(): { pending: string[], lastEventAt: number | null, results: number, resultAfterLastTurn: boolean } }}
 */
export function createSettleTracker({ quietMs }) {
  let rootSession = null;
  let lastEventAt = null;
  let results = 0;
  let resultAfterLastTurn = false;
  // Once the CLI has announced a list, it is the authority and the lifecycle fallback is ignored.
  let announced = false;
  const pending = new Set();

  const observe = (raw, nowMs) => {
    if (!raw || typeof raw !== "object") return;
    lastEventAt = nowMs;
    const event = normalizeEvent(raw);
    if (event.sessionId && !rootSession) rootSession = event.sessionId;

    if (event.kind === "result") {
      results += 1;
      resultAfterLastTurn = true;
      return;
    }
    if (event.kind === "assistant") {
      if (!isSubagentEvent(event, rootSession)) resultAfterLastTurn = false;
      return;
    }

    const list = backgroundTasksOf(raw);
    if (list) {
      announced = true;
      pending.clear();
      for (const id of list) pending.add(id);
      return;
    }
    if (announced) return;

    const task = backgroundTaskOf(raw);
    if (!task?.id) return;
    if (raw.subtype === "task_started") {
      if (raw.owned_by_subagent !== true) pending.add(task.id);
    } else if (raw.subtype === "task_notification") {
      pending.delete(task.id);
    } else if (raw.subtype === "task_updated" && TERMINAL_STATUSES.has(String(task.description).toLowerCase())) {
      pending.delete(task.id);
    }
  };

  const isSettled = (nowMs) =>
    Number.isFinite(quietMs) &&
    resultAfterLastTurn &&
    pending.size === 0 &&
    lastEventAt !== null &&
    nowMs - lastEventAt >= quietMs;

  const state = () => ({ pending: [...pending], lastEventAt, results, resultAfterLastTurn });

  return { observe, isSettled, state };
}
