import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  agentsOf,
  boardSnapshot,
  dependencyNames,
  describeEvent,
  isStreamFileName,
  newestStream,
  runIdOfStream,
  titleOf,
} from "../lib/monitor.mjs";

const FIXTURES = join(dirname(fileURLToPath(import.meta.url)), "fixtures", "claude-stream");
const describeAll = (lines) => {
  const state = {};
  return { rows: lines.flatMap((l) => describeEvent(l, state)), state };
};

// --- describeEvent --------------------------------------------------------------------------

test("a captured stream describes its skill, delegation, subagent call and result", () => {
  const lines = readFileSync(join(FIXTURES, "2.1.285", "delegation-and-skill.jsonl"), "utf8").split("\n");
  const { rows, state } = describeAll(lines);
  const skill = rows.find((r) => r.kind === "skill");
  assert.deepEqual({ who: skill.who, agent: skill.agent, text: skill.text }, { who: "lead", agent: null, text: "coding-conventions" });
  const delegation = rows.find((r) => r.kind === "delegate");
  assert.equal(delegation.text, "general-purpose");
  // The specialist's own call is filed under the agent the lead started, by that call's id.
  const subCall = rows.find((r) => r.sub && r.kind === "tool");
  assert.equal(subCall.who, "general-purpose");
  assert.equal(subCall.agent, delegation.spawns);
  // The agent's notification is its final report, and ends it.
  const report = rows.find((r) => r.kind === "report");
  assert.equal(report.agent, delegation.spawns);
  const [agent] = agentsOf(state);
  assert.equal(agent.status, "done");
  assert.equal(agent.tracked, true);
  const result = rows.at(-1);
  assert.equal(result.kind, "result");
  assert.equal(result.ok, true);
  assert.equal(result.turns, 4);
  assert.ok(state.turns >= 1 && state.subagentTurns >= 1);
  // Every row has its own increasing sequence number, which is how a feed orders and dedupes.
  assert.deepEqual(rows.map((r) => r.seq), rows.map((_, i) => i + 1));
});

test("a malformed or uninteresting line describes nothing", () => {
  const state = {};
  assert.deepEqual(describeEvent("not json", state), []);
  assert.deepEqual(describeEvent("", state), []);
  assert.deepEqual(describeEvent(JSON.stringify({ type: "user", message: { content: [] } }), state), []);
  assert.deepEqual(describeEvent(JSON.stringify({ type: "system", subtype: "task_progress" }), state), []);
});

const assistant = (id, content, parent = null) =>
  JSON.stringify({ type: "assistant", session_id: "s", parent_tool_use_id: parent, message: { id, content } });

test("repeated prose collapses and a repeated tool call is shown once", () => {
  const { rows } = describeAll([
    assistant("m1", [{ type: "text", text: "Reading the brief" }]),
    assistant("m1", [{ type: "text", text: "Reading the brief" }]),
    assistant("m2", [{ type: "tool_use", id: "t1", name: "Read", input: { file_path: "a.cs" } }]),
    assistant("m2", [{ type: "tool_use", id: "t1", name: "Read", input: { file_path: "a.cs" } }]),
  ]);
  assert.deepEqual(rows.map(({ kind, text, detail, agent }) => ({ kind, text, detail, agent })), [
    { kind: "text", text: "Reading the brief", detail: undefined, agent: null },
    { kind: "tool", text: "Read", detail: "a.cs", agent: null },
  ]);
});

const system = (subtype, fields) => JSON.stringify({ type: "system", subtype, session_id: "s", ...fields });
const spawn = (msg, id, type, extra = {}) =>
  assistant(msg, [{ type: "tool_use", id, name: "Agent", input: { subagent_type: type, description: `${type} work`, ...extra } }]);

test("each agent keeps its own log, stats and status, and a second of a kind is numbered", () => {
  const { rows, state } = describeAll([
    spawn("m1", "A1", "code-reviewer"),
    spawn("m2", "A2", "code-reviewer"),
    system("task_started", { task_id: "k1", tool_use_id: "A1", task_type: "local_agent", description: "code-reviewer work" }),
    system("task_progress", { task_id: "k1", tool_use_id: "A1", description: "code-reviewer work", usage: { tool_uses: 0 } }),
    system("task_progress", { task_id: "k1", tool_use_id: "A1", description: "Reading the diff", last_tool_name: "Bash", usage: { tool_uses: 3, total_tokens: 9000, duration_ms: 4000 } }),
    assistant("s1", [{ type: "tool_use", id: "t9", name: "Grep", input: { pattern: "x" } }], "A2"),
    system("task_notification", { task_id: "k1", tool_use_id: "A1", status: "completed", summary: "**APPROVED.**\n\nNothing to change." }),
    system("task_updated", { task_id: "k1", patch: { status: "failed" } }),
  ]);
  const [first, second] = agentsOf(state);
  assert.equal(first.label, "code-reviewer");
  assert.equal(second.label, "code-reviewer #2");
  assert.deepEqual(
    { status: first.status, toolUses: first.toolUses, tokens: first.tokens, durationMs: first.durationMs, hasReport: first.hasReport, streamed: first.streamed },
    { status: "done", toolUses: 3, tokens: 9000, durationMs: 4000, hasReport: true, streamed: 0 },
    "a status patch after the notification does not reopen it",
  );
  assert.equal(second.status, "running");
  assert.equal(second.streamed, 1);

  const firstLog = rows.filter((r) => r.agent === "A1").map((r) => [r.kind, r.text]);
  // The progress that only repeats the description is not a row; the report keeps its paragraphs.
  assert.deepEqual(firstLog, [["progress", "Reading the diff"], ["report", "**APPROVED.**\n\nNothing to change."]]);
  assert.deepEqual(rows.filter((r) => r.agent === "A2").map((r) => r.text), ["Grep"]);
  // A task with no agent behind it is the lead's background shell work.
  assert.equal(rows.filter((r) => r.kind === "task").length, 0);
});

test("the session's result ends only agents the CLI does not track itself", () => {
  const result = JSON.stringify({ type: "result", subtype: "success", is_error: false, total_cost_usd: 1, num_turns: 2, result: "" });
  const { state } = describeAll([
    spawn("m1", "A1", "qa-engineer"),
    spawn("m2", "A2", "security-reviewer"),
    system("task_started", { task_id: "k2", tool_use_id: "A2", task_type: "local_agent" }),
    result,
  ]);
  const [legacy, tracked] = agentsOf(state);
  assert.equal(legacy.status, "done");
  assert.equal(tracked.status, "running", "an async agent may still be working after the lead's first answer");
});

test("a background shell command shows as it starts and ends", () => {
  const { rows } = describeAll([
    system("task_started", { task_id: "b1", tool_use_id: "T1", task_type: "local_bash", description: "npm run gate" }),
    system("task_progress", { task_id: "b1", tool_use_id: "T1", description: "still running" }),
    system("task_notification", { task_id: "b1", tool_use_id: "T1", status: "completed", summary: "gate passed" }),
  ]);
  assert.deepEqual(rows.map((r) => [r.kind, r.text, r.agent]), [
    ["task", "background started", null],
    ["task", "background completed", null],
  ]);
});

test("thinking and tool results are never described", () => {
  const { rows } = describeAll([
    assistant("m1", [{ type: "thinking", thinking: "private" }]),
    JSON.stringify({ type: "user", message: { content: [{ type: "tool_result", content: "secret file body" }] } }),
  ]);
  assert.deepEqual(rows, []);
});

test("an error result carries its subtype", () => {
  const { rows } = describeAll([
    JSON.stringify({ type: "result", subtype: "error_max_turns", is_error: true, total_cost_usd: 2, num_turns: 9, result: "" }),
  ]);
  assert.equal(rows[0].ok, false);
  assert.match(rows[0].text, /error_max_turns/);
  assert.equal(rows[0].costUsd, 2);
});

// --- names ----------------------------------------------------------------------------------

test("only a run's stream file name is accepted", () => {
  assert.equal(isStreamFileName("run-2026-10-12T10-53-43-943Z-attempt-1.stream.jsonl"), true);
  for (const bad of [
    "../.env",
    "..%2F.env",
    "run-x-attempt-1.stream.jsonl/../../.env",
    "D:\\run-x-attempt-1.stream.jsonl",
    "/tmp/run-x-attempt-1.stream.jsonl",
    "run-x-attempt-1.log",
    "journal.jsonl",
    null,
  ]) {
    assert.equal(isStreamFileName(bad), false, String(bad));
  }
  assert.equal(runIdOfStream("run-2026-10-12T10-53-43-943Z-attempt-12.stream.jsonl"), "run-2026-10-12T10-53-43-943Z");
});

test("the newest stream is picked by modification time, ignoring other files", () => {
  const newest = newestStream([
    { name: "run-a-attempt-1.stream.jsonl", mtimeMs: 10 },
    { name: "drain.log", mtimeMs: 99 },
    { name: "run-b-attempt-1.stream.jsonl", mtimeMs: 20 },
  ]);
  assert.equal(newest.name, "run-b-attempt-1.stream.jsonl");
  assert.equal(newestStream([]), null);
});

test("Depends-on names resolve within the task's spec, as the queue resolves them", () => {
  const text = "Add the thing\nDepends-on: 01-a, other/02-b.md, none\n\nbody";
  assert.deepEqual(dependencyNames(text, "hrm/03-c"), ["hrm/01-a", "other/02-b"]);
  assert.deepEqual(dependencyNames("Title only", "hrm/03-c"), []);
  assert.equal(titleOf("\n# Add the thing\nmore"), "Add the thing");
});

// --- boardSnapshot --------------------------------------------------------------------------

const task = (file, title, deps) => ({ file, text: `${title}\n${deps ? `Depends-on: ${deps}\n` : ""}` });

test("the board groups tasks by spec, prefers doing over todo, and joins the journal", () => {
  const snap = boardSnapshot({
    lanes: {
      todo: [task("hrm/02-b.md", "B", "01-a"), task("hrm/03-c.md", "C", "02-b")],
      doing: [task("hrm/02-b.md", "B", "01-a")],
      done: [task("hrm/01-a.md", "A")],
      failed: [],
    },
    specs: ["hrm", "billing"],
    runs: [
      { id: "run-1", taskSlug: "hrm/01-a", outcome: "committed", costUsd: 3, attempts: 1, turns: [10] },
      { id: "run-2", taskSlug: "hrm/02-b", outcome: "blocked", failure: "session limit", costUsd: 5, attempts: 2,
        phases: [{ delegations: [{ name: "ui-ux-reviewer" }, { name: "frontend-engineer" }] }] },
      { kind: "budget-reset", taskSlug: "hrm/02-b" },
      { id: "run-0", task: "C\nDepends-on: 02-b", outcome: "failed", costUsd: 1, attempts: 1 },
    ],
    live: { name: "run-3-attempt-1.stream.jsonl", mtimeMs: 0 },
  });
  const hrm = snap.specs.find((s) => s.name === "hrm");
  assert.deepEqual(hrm.counts, { todo: 1, doing: 1, done: 1, failed: 0 });
  assert.equal(snap.specs[0].name, "hrm", "the spec with work in flight comes first");
  assert.ok(snap.specs.some((s) => s.name === "billing" && s.tasks.length === 0));

  const b = hrm.tasks.find((t) => t.stem === "hrm/02-b");
  assert.equal(b.lane, "doing");
  assert.deepEqual(b.deps, [{ stem: "hrm/01-a", landed: true }]);
  assert.equal(b.attempts, 2);
  assert.equal(b.lastOutcome, "blocked");
  assert.deepEqual(b.runs[0].delegations, ["ui-ux-reviewer", "frontend-engineer"]);

  // An entry with no slug is matched by its brief's first line.
  const c = hrm.tasks.find((t) => t.stem === "hrm/03-c");
  assert.equal(c.runs.length, 1);
  assert.deepEqual(c.deps, [{ stem: "hrm/02-b", landed: false }]);

  assert.equal(snap.totals.costUsd, 9);
  // A stream not yet in the journal belongs to the task being built.
  assert.deepEqual({ stem: snap.live.stem, finished: snap.live.finished }, { stem: "hrm/02-b", finished: false });
});

test("a finished stream is attributed through the journal", () => {
  const snap = boardSnapshot({
    lanes: { done: [task("hrm/01-a.md", "A")] },
    runs: [{ id: "run-1", taskSlug: "hrm/01-a", outcome: "committed", attempts: 1 }],
    live: { name: "run-1-attempt-1.stream.jsonl", mtimeMs: 0 },
  });
  assert.deepEqual({ stem: snap.live.stem, finished: snap.live.finished }, { stem: "hrm/01-a", finished: true });
});

test("the drain is running, stale or idle as the lock and its liveness say", () => {
  const lock = { pid: 1, host: "h", startedAt: "s", heartbeatAt: "b" };
  assert.equal(boardSnapshot({ lock, lockHeld: true }).drain.state, "running");
  assert.equal(boardSnapshot({ lock, lockHeld: false }).drain.state, "stale");
  assert.equal(boardSnapshot({}).drain.state, "idle");
});
