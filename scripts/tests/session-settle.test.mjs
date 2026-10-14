import { strict as assert } from "node:assert";
import { test } from "node:test";
import { createSettleTracker } from "../lib/session-settle.mjs";

const QUIET = 1_000;
const init = { type: "system", subtype: "init", session_id: "s", claude_code_version: "2.1.300" };
const turn = (id) => ({ type: "assistant", parent_tool_use_id: null, session_id: "s", message: { id, content: [] } });
const subagentTurn = (id) => ({ type: "assistant", parent_tool_use_id: "toolu_a", session_id: "s", message: { id, content: [] } });
const result = (subtype = "success") => ({ type: "result", subtype, is_error: subtype !== "success", num_turns: 1, total_cost_usd: 1 });
const pendingList = (...ids) => ({ type: "system", subtype: "background_tasks_changed", tasks: ids.map((task_id) => ({ task_id, task_type: "local_agent" })) });
const started = (task_id, extra = {}) => ({ type: "system", subtype: "task_started", task_id, task_type: "local_agent", ...extra });
const notified = (task_id) => ({ type: "system", subtype: "task_notification", task_id, status: "completed" });
const updated = (task_id, status) => ({ type: "system", subtype: "task_updated", task_id, patch: { status } });

/** Feed events one millisecond apart from t=0, returning the tracker and the last event's time. */
function feed(events) {
  const tracker = createSettleTracker({ quietMs: QUIET });
  events.forEach((raw, i) => tracker.observe(raw, i));
  return { tracker, last: events.length - 1 };
}

test("a session with no result yet has not settled, however quiet it is", () => {
  const { tracker, last } = feed([init, turn("m1")]);
  assert.equal(tracker.isSettled(last + QUIET * 10), false);
});

test("a final result with nothing pending settles once the stream has been quiet", () => {
  const { tracker, last } = feed([init, turn("m1"), result()]);
  assert.equal(tracker.isSettled(last + QUIET - 1), false, "still inside the quiet window");
  assert.equal(tracker.isSettled(last + QUIET), true);
});

test("the first result is not the end while reviewers are still running in the background", () => {
  // The lead dispatches a reviewer in the background and ends its turn: a result, but the
  // session will be woken by the reviewer's report.
  const { tracker, last } = feed([init, turn("m1"), pendingList("a1"), started("a1", { is_backgrounded: true }), turn("m2"), result()]);
  assert.equal(tracker.isSettled(last + QUIET * 10), false);
  assert.deepEqual(tracker.state().pending, ["a1"]);
});

test("a wake-up after a result starts a new turn, and only its own result can settle it", () => {
  const tracker = createSettleTracker({ quietMs: QUIET });
  [init, turn("m1"), pendingList("a1"), result(), pendingList(), turn("m2")].forEach((raw, i) => tracker.observe(raw, i));
  assert.equal(tracker.isSettled(5 + QUIET * 10), false, "the parent is mid-turn again");
  tracker.observe(result(), 6);
  assert.equal(tracker.isSettled(6 + QUIET), true);
});

test("any event resets the quiet clock", () => {
  const tracker = createSettleTracker({ quietMs: QUIET });
  [init, turn("m1"), result()].forEach((raw, i) => tracker.observe(raw, i));
  tracker.observe({ type: "system", subtype: "hook_started" }, 900);
  assert.equal(tracker.isSettled(2 + QUIET), false);
  assert.equal(tracker.isSettled(900 + QUIET), true);
});

test("a subagent's turn does not reopen the parent's", () => {
  const { tracker, last } = feed([init, turn("m1"), result(), subagentTurn("x1")]);
  assert.equal(tracker.isSettled(last + QUIET), true);
});

test("the announced list is the whole pending set, replacing what came before", () => {
  const { tracker, last } = feed([init, pendingList("a1", "a2"), pendingList("a2"), turn("m1"), result()]);
  assert.deepEqual(tracker.state().pending, ["a2"]);
  assert.equal(tracker.isSettled(last + QUIET), false);
  tracker.observe(pendingList(), last + 1);
  assert.equal(tracker.isSettled(last + 1 + QUIET), true);
});

test("once a list is announced, the task lifecycle no longer moves the pending set", () => {
  // A foreground task the announced list never names must not hold the session open.
  const { tracker, last } = feed([init, pendingList(), started("b1"), turn("m1"), result()]);
  assert.deepEqual(tracker.state().pending, []);
  assert.equal(tracker.isSettled(last + QUIET), true);
});

test("without announced lists, a started task is pending until its notification", () => {
  const { tracker, last } = feed([init, started("a1", { is_backgrounded: true }), turn("m1"), result()]);
  assert.equal(tracker.isSettled(last + QUIET), false);
  tracker.observe(notified("a1"), last + 1);
  tracker.observe(result(), last + 2);
  assert.equal(tracker.isSettled(last + 2 + QUIET), true);
});

test("without announced lists, a terminal status update also ends a task", () => {
  const { tracker } = feed([init, started("a1"), started("a2"), updated("a1", "completed"), updated("a2", "running"), turn("m1"), result()]);
  assert.deepEqual(tracker.state().pending, ["a2"], "running is not terminal");
});

test("a task a subagent started is its own business", () => {
  const { tracker, last } = feed([init, started("b1", { owned_by_subagent: true, parent_task_id: "a1" }), turn("m1"), result()]);
  assert.deepEqual(tracker.state().pending, []);
  assert.equal(tracker.isSettled(last + QUIET), true);
});

test("an error result settles too: the runner, not the tracker, decides what it means", () => {
  const { tracker, last } = feed([init, turn("m1"), result("error_during_execution")]);
  assert.equal(tracker.isSettled(last + QUIET), true);
});

test("an infinite quiet period never settles, and malformed input is ignored", () => {
  const tracker = createSettleTracker({ quietMs: Infinity });
  for (const raw of [null, "text", 42, init, turn("m1"), result()]) tracker.observe(raw, 0);
  assert.equal(tracker.isSettled(Number.MAX_SAFE_INTEGER), false);
  assert.equal(tracker.state().results, 1);
});

test("a list whose shape moved is not read as nothing pending", () => {
  const { tracker, last } = feed([init, started("a1"), { type: "system", subtype: "background_tasks_changed", tasks: "a1" }, turn("m1"), result()]);
  assert.deepEqual(tracker.state().pending, ["a1"], "the malformed list was ignored, the lifecycle still counts");
  assert.equal(tracker.isSettled(last + QUIET), false);
});
