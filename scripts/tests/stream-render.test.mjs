import { strict as assert } from "node:assert";
import { Readable } from "node:stream";
import { test } from "node:test";

import { isRefusal, renderEvent, renderStream } from "../lib/stream-render.mjs";

const assistant = (content) => JSON.stringify({ type: "assistant", message: { content } });

test("assistant prose is rendered as a progress line", () => {
  const out = renderEvent(assistant([{ type: "text", text: "Reading the schema first." }]));
  assert.match(out, /· Reading the schema first\./);
});

test("a tool call names the tool and the detail that identifies it", () => {
  const out = renderEvent(assistant([{ type: "tool_use", name: "Read", input: { file_path: "src/a.ts" } }]));
  assert.match(out, /→ Read {2}src\/a\.ts/);
});

test("Bash prefers its description over the raw command", () => {
  const out = renderEvent(
    assistant([{ type: "tool_use", name: "Bash", input: { description: "Run the gate", command: "npm run gate" } }]),
  );
  assert.match(out, /→ Bash {2}Run the gate/);
  assert.ok(!out.includes("npm run gate"));
});

test("an unknown tool still gets a line, just without detail", () => {
  const out = renderEvent(assistant([{ type: "tool_use", name: "SomethingNew", input: { x: 1 } }]));
  assert.equal(out.trim(), "→ SomethingNew");
});

test("the result event reports success and cost", () => {
  const out = renderEvent(JSON.stringify({ type: "result", is_error: false, total_cost_usd: 1.234, num_turns: 7 }));
  assert.match(out, /✓ done/);
  assert.match(out, /7 turns/);
  assert.match(out, /\$1\.23/);
});

test("an errored result is marked as such", () => {
  const out = renderEvent(JSON.stringify({ type: "result", is_error: true, total_cost_usd: 0.5 }));
  assert.match(out, /✗ ended with an error/);
});

test("a result with no cost figure still renders", () => {
  assert.match(renderEvent(JSON.stringify({ type: "result", is_error: false })), /✓ done/);
});

test("malformed and irrelevant lines are skipped, never thrown", () => {
  assert.equal(renderEvent("not json at all"), null);
  assert.equal(renderEvent("null"), null);
  assert.equal(renderEvent(JSON.stringify({ type: "system", subtype: "init" })), null);
  assert.equal(renderEvent(JSON.stringify({ type: "assistant", message: {} })), null);
});

test("long text is truncated rather than flooding the log", () => {
  const out = renderEvent(assistant([{ type: "text", text: "y".repeat(500) }]));
  assert.ok(out.length < 200, `expected a short line, got ${out.length}`);
  assert.ok(out.endsWith("…"));
});

test("repeated identical prose collapses to one line", () => {
  const state = {};
  const first = renderEvent(assistant([{ type: "text", text: "same" }]), state);
  const second = renderEvent(assistant([{ type: "text", text: "same" }]), state);
  assert.ok(first);
  assert.equal(second, null);
});

test("renderStream returns the cost from the result event", async () => {
  const lines = [
    assistant([{ type: "text", text: "working" }]),
    JSON.stringify({ type: "result", is_error: false, total_cost_usd: 2.5 }),
  ].join("\n");
  const printed = [];
  const res = await renderStream(Readable.from([lines]), (s) => printed.push(s));
  assert.equal(res.costUsd, 2.5);
  assert.equal(res.isError, false);
  assert.ok(printed.some((l) => l.includes("working")));
});

test("a feed with no result event reports unknown cost, not zero", async () => {
  const res = await renderStream(Readable.from([assistant([{ type: "text", text: "x" }])]), () => {});
  assert.equal(res.costUsd, null, "null means unknown — a ceiling must not read it as free");
});

test("events split across chunk boundaries are still parsed", async () => {
  const full = JSON.stringify({ type: "result", is_error: false, total_cost_usd: 3.75 });
  const res = await renderStream(Readable.from([full.slice(0, 20), full.slice(20), "\n"]), () => {});
  assert.equal(res.costUsd, 3.75);
});

test("a stream error resolves rather than rejecting", async () => {
  const broken = new Readable({ read() { this.destroy(new Error("boom")); } });
  const res = await renderStream(broken, () => {});
  assert.equal(res.costUsd, null);
});

test("delegations to specialists are collected, in order", async () => {
  const lines = [
    assistant([{ type: "tool_use", name: "Task", input: { subagent_type: "db-migrator", description: "add tables" } }]),
    assistant([{ type: "tool_use", name: "Task", input: { subagent_type: "code-reviewer", description: "review" } }]),
    JSON.stringify({ type: "result", is_error: false, total_cost_usd: 1 }),
  ].join("\n");
  const res = await renderStream(Readable.from([lines]), () => {});
  assert.deepEqual(res.subagents, ["db-migrator", "code-reviewer"]);
});

test("a run that delegated nothing reports an empty list, not undefined", async () => {
  const res = await renderStream(
    Readable.from([JSON.stringify({ type: "result", is_error: false })]), () => {});
  assert.deepEqual(res.subagents, []);
});

test("a Task call with no subagent_type is not counted as a delegation", async () => {
  const line = assistant([{ type: "tool_use", name: "Task", input: { description: "vague" } }]);
  const res = await renderStream(Readable.from([line]), () => {});
  assert.deepEqual(res.subagents, []);
});

test("a delegation line names the specialist, so a watcher can see the routing", () => {
  const out = renderEvent(
    assistant([{ type: "tool_use", name: "Task", input: { subagent_type: "chat-pipeline", description: "fix the prompt" } }]),
  );
  assert.match(out, /→ Task {2}chat-pipeline: fix the prompt/);
});

test("Agent and Task both count as delegating to a specialist", async () => {
  const call = (name, type) =>
    assistant([{ type: "tool_use", name, input: { subagent_type: type, description: "d" } }]);
  const feed = [call("Agent", "db-migrator"), call("Task", "code-reviewer")].join("\n");
  const res = await renderStream(Readable.from([feed]), () => {});
  assert.deepEqual(res.subagents, ["db-migrator", "code-reviewer"]);
});

test("an Agent call renders with the specialist named, like Task does", () => {
  const out = renderEvent(
    assistant([{ type: "tool_use", name: "Agent", input: { subagent_type: "designer", description: "check states" } }]),
  );
  assert.match(out, /→ Agent {2}designer: check states/);
});

test("a run past the turn limit is stopped and says so", async () => {
  const turn = () => assistant([{ type: "text", text: "still going" }]);
  const feed = Array.from({ length: 6 }, turn).join("\n");
  const printed = [];
  let limitHit = null;
  const res = await renderStream(Readable.from([feed]), (s) => printed.push(s), {
    maxTurns: 3,
    onLimit: (info) => { limitHit = info; },
  });
  assert.ok(limitHit, "onLimit must fire");
  assert.equal(limitHit.maxTurns, 3);
  assert.equal(res.stoppedAtLimit, true);
  assert.ok(printed.some((l) => /exceeds the per-attempt limit/.test(l)));
});

test("the limit fires once, not on every turn past it", async () => {
  const feed = Array.from({ length: 10 }, () => assistant([{ type: "text", text: "x" }])).join("\n");
  let calls = 0;
  await renderStream(Readable.from([feed]), () => {}, { maxTurns: 2, onLimit: () => { calls += 1; } });
  assert.equal(calls, 1);
});

test("a run inside the limit is not stopped and reports its turn count", async () => {
  const feed = Array.from({ length: 3 }, () => assistant([{ type: "text", text: "x" }])).join("\n");
  const res = await renderStream(Readable.from([feed]), () => {}, { maxTurns: 10 });
  assert.equal(res.stoppedAtLimit, false);
  assert.equal(res.turns, 3);
});

test("with no limit given, nothing is ever stopped", async () => {
  const feed = Array.from({ length: 50 }, () => assistant([{ type: "text", text: "x" }])).join("\n");
  const res = await renderStream(Readable.from([feed]), () => {});
  assert.equal(res.stoppedAtLimit, false);
  assert.equal(res.turns, 50);
});

test("an account-level refusal is detected, not mistaken for a task failure", async () => {
  for (const text of [
    "You've hit your session limit · resets 7pm (Asia/Karachi)",
    "Your usage limit has been reached.",
    "quota exceeded for this organisation",
    "Your credit balance is too low",
  ]) {
    const feed = assistant([{ type: "text", text }]);
    const res = await renderStream(Readable.from([feed]), () => {});
    assert.ok(res.blocked, `should flag: ${text}`);
  }
});

test("ordinary prose is never mistaken for a refusal", async () => {
  for (const text of [
    "Reading the schema first.",
    "I will limit the query to ten rows.",
    "The rate of zero-result searches is falling.",
  ]) {
    const res = await renderStream(Readable.from([assistant([{ type: "text", text }])]), () => {});
    assert.equal(res.blocked, null, `should not flag: ${text}`);
  }
});

test("only the parent session's turns count toward the cap", async () => {
  const ev = (sid) => JSON.stringify({
    type: "assistant", session_id: sid, message: { content: [{ type: "text", text: "x" }] },
  });
  // Two parent turns around forty subagent turns. The subagent's work is not the parent's
  // budget: counting it killed nine runs that were delegating exactly as instructed.
  const feed = [ev("parent"), ...Array.from({ length: 40 }, () => ev("sub")), ev("parent")].join("\n");
  const res = await renderStream(Readable.from([feed]), () => {}, { maxTurns: 10 });
  assert.equal(res.turns, 2);
  assert.equal(res.stoppedAtLimit, false);
});

test("the parent alone can still trip the cap", async () => {
  const ev = () => JSON.stringify({
    type: "assistant", session_id: "parent", message: { content: [{ type: "text", text: "x" }] },
  });
  const res = await renderStream(Readable.from([Array.from({ length: 6 }, ev).join("\n")]), () => {}, {
    maxTurns: 3,
  });
  assert.equal(res.stoppedAtLimit, true);
});

test("being unable to reach the API counts as unable to run", async () => {
  for (const text of [
    "API Error: Can't reach the API server — check your internet or DNS (ENOTFOUND)",
    "connect ECONNREFUSED 160.79.104.10:443",
    "getaddrinfo EAI_AGAIN api.anthropic.com",
  ]) {
    const feed = assistant([{ type: "text", text }]);
    const res = await renderStream(Readable.from([feed]), () => {});
    assert.ok(res.blocked, `network failure should block: ${text}`);
  }
});

test("ordinary talk about limits and timeouts is not a refusal", async () => {
  for (const text of [
    "I will limit the query to ten rows.",
    "The timeout is set to 15 seconds.",
    "Rate of zero-result searches is falling.",
  ]) {
    const res = await renderStream(Readable.from([assistant([{ type: "text", text }])]), () => {});
    assert.equal(res.blocked, null, `should not block: ${text}`);
  }
});

test("a refusal must be short and error-shaped, not merely contain the words", async () => {
  const { isRefusal } = await import("../lib/stream-render.mjs");
  // Real refusals: short, no structure.
  for (const t of [
    "You've hit your session limit · resets 8:20pm (Asia/Karachi)",
    "API Error: Can't reach the API server — check your internet or DNS (ENOTFOUND)",
    "connect ECONNREFUSED 160.79.104.10:443",
  ]) assert.ok(isRefusal(t), `should block: ${t}`);

  // An agent's own report will eventually mention one of these in passing. One did, inside
  // a scope check about documentation files, and a healthy task was killed for it.
  for (const t of [
    "## Scope check\n\nConfirmed documentation-only. The rate limit note stays as written.",
    "- the timeout is set to 15 seconds\n- ETIMEDOUT is handled in the retry path",
    "I will limit the query to ten rows.",
    `A long report that happens to mention a session limit somewhere in the middle. ${"x".repeat(320)}`,
  ]) assert.ok(!isRefusal(t), `should not block: ${t.slice(0, 60)}`);
});

// --- the $108 bug ---------------------------------------------------------------------
//
// The pattern accepted `rate.?limit`, so it matched `hubspot-rate-limit.ts` — and the task
// whose whole subject was HubSpot rate limiting could never finish. Thirty restarts, six
// journal entries, $108.44, every one filed `blocked` for an account refusal that never
// happened. Both directions are pinned here so it cannot come back.

test("a filename containing rate-limit is not an account refusal", () => {
  for (const text of [
    "I'll name the new file `hubspot-rate-limit.ts` and its test alongside it",
    "Created apps/api/src/lib/hubspot-rate-limit.ts",
    "Adding rate_limit handling to the HubSpot client",
    "The hubspot-rate-limit module now backs off on 429",
  ]) {
    assert.equal(isRefusal(text), false, `wrongly read as a refusal: ${text}`);
  }
});

test("a real account refusal is still caught", () => {
  for (const text of [
    "You've hit your session limit · resets 1:20am (Asia/Karachi)",
    "Rate limited. Please try again later.",
    "Your credit balance is too low to run this request",
    "getaddrinfo ENOTFOUND api.anthropic.com",
    "quota exceeded",
  ]) {
    assert.equal(isRefusal(text), true, `missed a real refusal: ${text}`);
  }
});
