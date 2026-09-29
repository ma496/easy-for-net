/**
 * The Claude Code stream contract, held against real output.
 *
 * `stream-render.test.mjs` pins the parser's rules on events written by hand. This file
 * replays streams the CLI actually produced (`fixtures/claude-stream/<version>/`, recorded by
 * `npm run test:claude-contract -- --record`), so the rules are checked against the shape
 * the CLI really emits rather than the shape somebody remembered. The hand-written fixtures
 * cover only what a live probe cannot produce on demand.
 *
 * When a CLI release changes the format, record its fixtures. Each version folder is checked
 * against the same expectations, keyed by probe name, so a new folder that no longer
 * satisfies them fails here and names the field.
 */
import { strict as assert } from "node:assert";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { Readable } from "node:stream";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { isSubagentEvent, normalizeEvent, parseLine, schemaProblems, scrubEventLine } from "../lib/claude-events.mjs";
import { renderStream } from "../lib/stream-render.mjs";

const FIXTURES = join(dirname(fileURLToPath(import.meta.url)), "fixtures", "claude-stream");
const read = (...parts) => readFileSync(join(FIXTURES, ...parts), "utf8");
const replay = (text, limits) => renderStream(Readable.from([text]), () => {}, limits);
const lines = (text) => text.split("\n").filter((l) => l.trim());

const versions = readdirSync(FIXTURES, { withFileTypes: true })
  .filter((d) => d.isDirectory() && /^\d+\.\d+\.\d+/.test(d.name))
  .map((d) => d.name);

test("at least one recorded CLI version is on file", () => {
  assert.ok(versions.length > 0, "record one with: npm run test:claude-contract -- --record");
});

/** What each live probe must yield, whatever version recorded it. */
const EXPECT = {
  "delegation-and-skill": (res, text) => {
    assert.equal(typeof res.costUsd, "number", "cost is read from the result event");
    assert.ok(res.costUsd > 0);
    assert.equal(res.isError, false);
    assert.deepEqual(res.subagents, ["general-purpose"], "the session's own delegation is observed");
    assert.ok(res.skills.length >= 1, "the skill load is observed");
    assert.ok(res.subagentTurns > 0, "the subagent's turns are told apart from the parent's");
    // One API turn is several events (one per content block). Counting events would inflate
    // the turn cap's reading, so turns must be fewer than the parent's assistant events.
    const parentEvents = lines(text)
      .map(parseLine)
      .filter((e) => e?.type === "assistant" && e.parent_tool_use_id === null).length;
    assert.ok(res.turns > 0 && res.turns < parentEvents, `turns ${res.turns} vs ${parentEvents} parent events`);
    assert.equal(res.stoppedByCli, false);
    assert.equal(res.blocked, null);
  },
  "max-turns": (res) => {
    assert.equal(res.stoppedByCli, true, "--max-turns is recognised as a limit, not a crash");
    assert.equal(res.resultSubtype, "error_max_turns");
    assert.equal(res.isError, true);
    assert.equal(typeof res.costUsd, "number", "a session stopped by --max-turns still reports its cost");
    assert.equal(res.blocked, null, "a turn limit is not an account refusal");
  },
  "max-budget": (res) => {
    assert.equal(res.stoppedByCli, true, "--max-budget-usd is recognised as a limit, not a crash");
    assert.equal(res.resultSubtype, "error_max_budget_usd");
    assert.equal(typeof res.costUsd, "number", "a session stopped on its budget still reports its cost");
    assert.equal(res.blocked, null, "a budget stop is not an account refusal");
  },
};

for (const version of versions) {
  const files = readdirSync(join(FIXTURES, version)).filter((f) => f.endsWith(".jsonl"));

  for (const file of files) {
    const probe = file.replace(/\.jsonl$/, "");
    const text = read(version, file);

    test(`${version}/${probe}: every event carries the fields the runner reads`, () => {
      const problems = [];
      for (const [i, line] of lines(text).entries()) {
        const raw = parseLine(line);
        assert.ok(raw, `line ${i + 1} is not JSON`);
        for (const p of schemaProblems(raw)) problems.push(`line ${i + 1}: ${p}`);
      }
      assert.deepEqual(problems, []);
    });

    test(`${version}/${probe}: the version is read from the init event`, async () => {
      assert.equal((await replay(text)).cliVersion, version);
    });

    if (EXPECT[probe]) {
      test(`${version}/${probe}: yields what the runner decides on`, async () => {
        EXPECT[probe](await replay(text), text);
      });
    }
  }

  const delegation = join(FIXTURES, version, "delegation-and-skill.jsonl");
  if (!existsSync(delegation)) continue;
  const text = readFileSync(delegation, "utf8");

  test(`${version}: subagent events share the parent's session id, and parent_tool_use_id tells them apart`, () => {
    const events = lines(text).map(parseLine).filter((e) => e?.type === "assistant");
    const sessions = new Set(events.map((e) => e.session_id));
    assert.equal(sessions.size, 1, "if this changes, isSubagentEvent's fallback matters again");
    const root = events[0].session_id;
    const sub = events.filter((e) => isSubagentEvent(normalizeEvent(e), root));
    assert.ok(sub.length > 0);
    assert.ok(sub.every((e) => typeof e.parent_tool_use_id === "string"));
  });

  test(`${version}: a stream cut off before its result reports unknown cost, never zero`, async () => {
    const cut = lines(text).filter((l) => parseLine(l)?.type !== "result").join("\n");
    const res = await replay(cut);
    assert.equal(res.costUsd, null);
    assert.deepEqual(res.subagents, ["general-purpose"], "what was observed before the cut still counts");
  });

  test(`${version}: noise between events changes nothing`, async () => {
    const clean = await replay(text);
    const noisy = lines(text)
      .flatMap((l) => [l, "not json", '{"type":"brand_new_event","x":1}', "[1,2]", ""])
      .join("\n");
    const res = await replay(noisy);
    for (const k of ["costUsd", "isError", "turns", "subagentTurns", "resultSubtype"]) {
      assert.deepEqual(res[k], clean[k], k);
    }
    assert.deepEqual(res.subagents, clean.subagents);
    assert.deepEqual(res.skills, clean.skills);
  });

  test(`${version}: the backstop turn cap counts the parent only`, async () => {
    const clean = await replay(text);
    const atCap = await replay(text, { maxTurns: clean.turns });
    assert.equal(atCap.stoppedAtLimit, false, "a subagent's turns must not push the parent over");
    const below = await replay(text, { maxTurns: clean.turns - 1 });
    assert.equal(below.stoppedAtLimit, true);
  });
}

// --- hand-written shapes --------------------------------------------------------------

test("an older client: Task, no parent_tool_use_id, subagents on their own session", async () => {
  const res = await replay(read("synthetic", "legacy-task.jsonl"));
  assert.deepEqual(res.subagents, ["data-engineer", "code-reviewer"]);
  assert.deepEqual(res.skills, ["backend-entity"], "the older `name` field is still a skill name");
  assert.equal(res.turns, 4, "no message ids: each parent event is a turn");
  assert.equal(res.subagentTurns, 2);
  assert.equal(res.costUsd, 1.5);
});

test("a refusal in the assistant's text blocks the run", async () => {
  const res = await replay(read("synthetic", "refusal.jsonl"));
  assert.match(res.blocked ?? "", /session limit/);
});

test("a refusal that appears only in the error result blocks the run", async () => {
  const res = await replay(read("synthetic", "result-refusal.jsonl"));
  assert.match(res.blocked ?? "", /Credit balance/);
  assert.equal(res.resultSubtype, "error_during_execution");
});

test("a subagent's own delegation is not credited to the session", async () => {
  const res = await replay(read("synthetic", "nested-delegation.jsonl"));
  assert.deepEqual(res.subagents, ["backend-engineer"], "code-reviewer was called by the builder, not the lead");
  assert.deepEqual(res.skills, ["backend-endpoint"], "a skill a builder loaded still counts as read");
  assert.equal(res.turns, 2, "three events of msg_p1 are one turn");
  assert.equal(res.subagentTurns, 3);
});

test("a tool block repeated across events of one message is counted once", async () => {
  const res = await replay(read("synthetic", "nested-delegation.jsonl"));
  assert.equal(res.subagents.filter((s) => s === "backend-engineer").length, 1);
});

// --- the scrubber -----------------------------------------------------------------------

test("scrubbing keeps what the runner reads and drops what a fixture must not carry", () => {
  const line = JSON.stringify({
    type: "assistant",
    parent_tool_use_id: null,
    session_id: "s",
    message: {
      id: "m",
      content: [
        { type: "thinking", thinking: "secret reasoning", signature: "sig" },
        { type: "tool_use", id: "t", name: "Agent", input: { subagent_type: "qa-engineer", prompt: "long brief", description: "d" } },
        { type: "text", text: "Reading D:\\work\\repo\\a.ts" },
      ],
    },
  });
  const out = JSON.parse(scrubEventLine(line, [["D:\\work\\repo", "<repo>"]]));
  assert.equal(out.message.content[0].thinking, "[scrubbed]");
  assert.equal(out.message.content[0].signature, undefined);
  assert.equal(out.message.content[1].input.subagent_type, "qa-engineer");
  assert.equal(out.message.content[1].input.prompt, "[scrubbed]");
  assert.equal(out.message.content[2].text, "Reading <repo>\\a.ts");
  assert.deepEqual(schemaProblems(out), []);
});

test("scrubbing reduces init to its identifying fields", () => {
  const out = JSON.parse(
    scrubEventLine(JSON.stringify({ type: "system", subtype: "init", session_id: "s", claude_code_version: "9.9.9", tools: ["x"], cwd: "/home/me" })),
  );
  assert.deepEqual(Object.keys(out).sort(), ["claude_code_version", "session_id", "subtype", "type"]);
});

test("schemaProblems names the field a release dropped", () => {
  assert.deepEqual(schemaProblems({ type: "result", is_error: false, num_turns: 1, subtype: "success" }), [
    "result: `total_cost_usd` is not a number",
  ]);
  assert.deepEqual(
    schemaProblems({ type: "assistant", parent_tool_use_id: null, message: { id: "m", content: [{ type: "tool_use", name: "Agent", input: {} }] } }),
    ["Agent: `input.subagent_type` is missing"],
  );
});
