/**
 * The one place that knows what `claude -p --output-format stream-json --verbose` emits.
 *
 * Everything the runner decides from a session — what it cost, whether it errored, how many
 * turns it took, which specialists it delegated to, which skills it loaded, whether the
 * account refused it — is read out of this feed. The feed belongs to the CLI, not to us, and
 * it changes between releases. When it does, this file is the only one that should need to
 * change, and `tests/claude-stream-contract.test.mjs` is what says so: it replays streams
 * captured from real CLI versions (`tests/fixtures/claude-stream/<version>/`) through here.
 *
 * What a captured 2.1.x stream actually looks like, because two of these facts were assumed
 * wrongly for a long time:
 *
 *   - **A subagent's events share the parent's `session_id`.** What marks them is
 *     `parent_tool_use_id`: `null` on the parent's own events, the id of the `Agent` call that
 *     spawned it on a subagent's. Telling them apart by session id counted every subagent
 *     turn against the parent's turn cap.
 *   - **One API turn arrives as several `assistant` events**, one per content block
 *     (thinking, text, each tool call), all carrying the same `message.id`. Counting events
 *     counted blocks, not turns.
 *   - A session can end with **more than one `result` event** (`result_index` 0, 1, …) when a
 *     background subagent reports after the parent's first answer. `total_cost_usd` is
 *     cumulative, so the last one is the figure to keep.
 *
 * Every function is total: an unrecognised or malformed line comes back as `kind: "unknown"`
 * rather than throwing. A parser that can crash would take down the run it is reading.
 */

/** The tool names that mean "hand this to a specialist". `Task` is the older name; both count. */
export const DELEGATION_TOOLS = new Set(["Agent", "Task"]);

/** The tool that loads a skill, and where its name lives in the call's input. */
export const SKILL_TOOL = "Skill";
export const skillNameOf = (input) => {
  const named = input?.skill ?? input?.name;
  return named ? String(named) : null;
};

/** Parse one line of the feed. Anything that is not a JSON object is null. */
export function parseLine(line) {
  if (typeof line !== "string" || !line.trim()) return null;
  try {
    const value = JSON.parse(line);
    return value && typeof value === "object" && !Array.isArray(value) ? value : null;
  } catch {
    return null; // A warning on stderr redirected in, or a partial write.
  }
}

const KINDS = new Set(["assistant", "user", "result", "system"]);

/**
 * Reduce one raw event to the fields the runner reads.
 *
 * `parentToolUseId` keeps the distinction between *absent* (`undefined` — an older client, or
 * a hand-written event, that never says) and *the parent* (`null`). Only the first falls back
 * to comparing session ids.
 */
export function normalizeEvent(raw) {
  if (!raw || typeof raw !== "object") return { kind: "unknown", raw };
  const kind = raw.type === "rate_limit_event" ? "rate_limit" : KINDS.has(raw.type) ? raw.type : "unknown";
  const event = {
    kind,
    subtype: typeof raw.subtype === "string" ? raw.subtype : null,
    sessionId: typeof raw.session_id === "string" ? raw.session_id : null,
    parentToolUseId: "parent_tool_use_id" in raw ? raw.parent_tool_use_id ?? null : undefined,
    messageId: typeof raw.message?.id === "string" ? raw.message.id : null,
    texts: [],
    toolUses: [],
    result: null,
    raw,
  };

  if (kind === "assistant") {
    const content = Array.isArray(raw.message?.content) ? raw.message.content : [];
    for (const block of content) {
      if (block?.type === "text" && typeof block.text === "string" && block.text.trim()) {
        event.texts.push(block.text);
      } else if (block?.type === "tool_use" && typeof block.name === "string") {
        event.toolUses.push({
          id: typeof block.id === "string" ? block.id : null,
          name: block.name,
          input: block.input && typeof block.input === "object" ? block.input : {},
        });
      }
    }
  }

  if (kind === "result") {
    event.result = {
      costUsd: typeof raw.total_cost_usd === "number" && Number.isFinite(raw.total_cost_usd) ? raw.total_cost_usd : null,
      isError: Boolean(raw.is_error),
      subtype: event.subtype,
      numTurns: typeof raw.num_turns === "number" ? raw.num_turns : null,
      text: typeof raw.result === "string" ? raw.result : "",
      apiErrorStatus: raw.api_error_status ?? null,
      terminalReason: typeof raw.terminal_reason === "string" ? raw.terminal_reason : null,
    };
  }

  return event;
}

/**
 * Whether an event is a subagent's rather than the session's own.
 *
 * `rootSession` is the first session id the feed named. It is consulted only when the event
 * does not carry `parent_tool_use_id` at all; an event with no session id either is the
 * parent's, since a feed that never identifies a session is a single session by definition.
 */
export function isSubagentEvent(event, rootSession) {
  if (event.parentToolUseId !== undefined) return event.parentToolUseId !== null;
  if (!event.sessionId || !rootSession) return false;
  return event.sessionId !== rootSession;
}

/**
 * The result subtypes that mean the session stopped itself on a limit rather than finishing.
 * `error_max_turns` is what `--max-turns` produces; it still carries a cost, which is why
 * the runner passes that flag rather than relying on killing the process.
 */
export const LIMIT_SUBTYPES = new Set(["error_max_turns", "error_max_budget_usd"]);

/**
 * What a contract test checks on every captured event: the fields the runner reads, present
 * and of the type it reads them as. Returns one message per violation, naming the field, so
 * a CLI release that renames one fails with the name rather than with a total that is quietly
 * zero.
 */
export function schemaProblems(raw) {
  const problems = [];
  if (!raw || typeof raw !== "object") return ["not a JSON object"];
  if (typeof raw.type !== "string") problems.push("`type` is missing");

  if (raw.type === "assistant") {
    if (!Array.isArray(raw.message?.content)) problems.push("assistant: `message.content` is not an array");
    if (!("parent_tool_use_id" in raw)) problems.push("assistant: `parent_tool_use_id` is missing");
    if (typeof raw.message?.id !== "string") problems.push("assistant: `message.id` is missing");
    for (const block of raw.message?.content ?? []) {
      if (block?.type !== "tool_use") continue;
      if (typeof block.name !== "string") problems.push("tool_use: `name` is missing");
      if (DELEGATION_TOOLS.has(block.name) && typeof block.input?.subagent_type !== "string") {
        problems.push(`${block.name}: \`input.subagent_type\` is missing`);
      }
      if (block.name === SKILL_TOOL && !skillNameOf(block.input)) problems.push("Skill: `input.skill` is missing");
    }
  }

  if (raw.type === "result") {
    if (typeof raw.total_cost_usd !== "number") problems.push("result: `total_cost_usd` is not a number");
    if (typeof raw.is_error !== "boolean") problems.push("result: `is_error` is not a boolean");
    if (typeof raw.num_turns !== "number") problems.push("result: `num_turns` is not a number");
    if (typeof raw.subtype !== "string") problems.push("result: `subtype` is missing");
  }

  if (raw.type === "system" && raw.subtype === "init" && typeof raw.claude_code_version !== "string") {
    problems.push("system/init: `claude_code_version` is missing");
  }
  return problems;
}

/**
 * Strip what a fixture must not carry — the machine's paths, file contents a tool returned,
 * the model's private reasoning, the long tool and plugin lists — and keep every field the
 * runner reads. A fixture is committed, so it is scrubbed as if it were going public.
 * `replacements` is `[[path, label], …]` — the repository and home folders, typically.
 */
export function scrubEventLine(line, replacements = []) {
  const raw = parseLine(line);
  if (!raw) return null;
  if (raw.type === "system" && raw.subtype === "init") {
    const { type, subtype, session_id, claude_code_version, model, permissionMode, uuid } = raw;
    return JSON.stringify({ type, subtype, session_id, claude_code_version, model, permissionMode, uuid });
  }
  if (raw.type === "system" && /^hook_/.test(raw.subtype ?? "")) {
    for (const k of ["output", "stdout", "stderr"]) if (k in raw) raw[k] = "";
  }
  if (raw.type === "system" && typeof raw.prompt === "string") raw.prompt = "[scrubbed]";
  // A background task's transcript path names the machine's temp folder and the project.
  if (typeof raw.output_file === "string") raw.output_file = "[scrubbed]";
  for (const block of Array.isArray(raw.message?.content) ? raw.message.content : []) {
    if (block?.type === "thinking") {
      block.thinking = "[scrubbed]";
      delete block.signature;
    }
    if (block?.type === "tool_result") block.content = "[scrubbed]";
    if (block?.type === "text" && raw.type === "user") block.text = "[scrubbed]";
    if (block?.type === "tool_use" && typeof block.input?.prompt === "string") block.input.prompt = "[scrubbed]";
  }
  if (raw.type === "user" && "tool_use_result" in raw) raw.tool_use_result = "[scrubbed]";
  if (raw.type === "result" && typeof raw.result === "string" && !raw.is_error) raw.result = "[scrubbed]";
  let text = JSON.stringify(raw);
  // Paths appear JSON-escaped; replace both spellings of each.
  for (const [path, label] of replacements) {
    for (const spelling of new Set([path, path.replace(/\\/g, "/")])) {
      text = text.split(JSON.stringify(spelling).slice(1, -1)).join(label);
    }
  }
  return text;
}
