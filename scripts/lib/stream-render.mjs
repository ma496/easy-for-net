/**
 * Turn a `claude --output-format stream-json` feed into something a person can watch.
 *
 * The spawn sites moved off `--output-format text` because text buffers the entire response
 * and writes nothing until the call returns — a healthy run sits silent for many minutes and
 * reads as a hang, which is exactly when somebody kills it. But raw stream-json is a wall of
 * one-line JSON objects, so streaming alone only trades silence for noise. This renders the
 * feed as progress lines instead.
 *
 * What it shows, and nothing else:
 *
 *   ·  the assistant's own prose, which is where its reasoning about the task appears
 *   →  each tool it reaches for, with the one detail that identifies the call
 *   ✓  the final result, with what the call cost
 *
 * Deliberately lossy. The point is a line every few seconds proving the run is alive and
 * roughly where it is — not a transcript. The full record already exists in the journal.
 *
 * Every parse is defensive: an unrecognised or malformed event is skipped rather than
 * thrown. A renderer that can crash would take down the run it is only supposed to narrate.
 */

/**
 * The run could not happen at all, as the model or the client reports it. Two shapes, and
 * both matter: **not allowed to** (a usage limit, an exhausted balance) and **could not
 * reach** (DNS, a refused connection, the API server down).
 *
 * Neither is this task's failure, and every task after it fails identically — so the caller
 * must be able to tell "this change is wrong" from "nothing can run right now". Missing the
 * second shape sent six tasks to failed/ at $0.00 each while the network was down.
 */
/**
 * Connectivity, as the runtime reports it. Read against the RAW text, never the stripped
 * copy — `EAI_AGAIN` is an identifier by shape and the stripper below would eat it, which
 * is a mistake in the expensive direction: a network outage read as ordinary prose sends
 * every remaining task to failed/ for something none of them caused.
 */
const NETWORK_REFUSAL =
  /can'?t reach the api server|unable to reach the api|\b(ENOTFOUND|ECONNREFUSED|ETIMEDOUT|EAI_AGAIN)\b/i;

/** Entitlement, which is written in prose and so is read against the stripped copy. */
const ENTITLEMENT_REFUSAL =
  /\b(session limit|usage limit|rate limit(?:ed)?|quota exceeded|credit balance|insufficient credit)\b/i;

/** Kept for callers that want the whole vocabulary in one pattern. */
export const ACCOUNT_REFUSAL = new RegExp(
  `${ENTITLEMENT_REFUSAL.source}|${NETWORK_REFUSAL.source}`,
  "i",
);

/**
 * Text the refusal test must not read: code spans, file paths and identifiers.
 *
 * This cost $108 and thirty restarts. The pattern used to accept `rate.?limit`, so it matched
 * `hubspot-rate-limit.ts` — and the task whose whole subject was HubSpot rate limiting could
 * therefore never finish. Every run wrote that filename, every run was read as the account
 * being refused, and the drain put it back and started again.
 *
 * Two defences, because either alone is thin: the pattern now wants "rate limit" as words with
 * a space, which a hyphenated or underscored identifier never is, and identifiers are stripped
 * before it looks at all. A refusal is written in prose; a filename is not.
 */
function withoutCode(text) {
  return String(text ?? "")
    .replace(/`[^`]*`/g, " ")                       // `hubspot-rate-limit.ts`
    .replace(/\S*[/\\]\S*/g, " ")                   // paths
    .replace(/\b[\w.]*\.(tsx?|mjs|js|json|md|html|css|cs|csproj|slnx)\b/gi, " ")  // bare filenames
    .replace(/\b\w+[-_]\w[\w-]*\b/g, " ");           // hyphen/underscore identifiers
}

/**
 * Whether a message is the run being refused, rather than an agent *mentioning* a refusal.
 *
 * The pattern alone is not enough. An agent's report is long prose and will eventually
 * contain "rate limit" or "timed out" in passing — one did, inside a scope check about
 * documentation files, and the task was killed for it. A real refusal is a short
 * error-shaped line: no markdown structure, and not a paragraph of reasoning.
 */
export function isRefusal(text) {
  const t = String(text ?? "").trim();
  if (t.length > 300) return false;
  if (/^#{1,6}\s|\n#{1,6}\s|^[-*]\s|\n\n/.test(t)) return false;
  return NETWORK_REFUSAL.test(t) || ENTITLEMENT_REFUSAL.test(withoutCode(t));
}

/** The tool names that mean "hand this to a specialist". Clients differ; both count. */
export const DELEGATION_TOOLS = new Set(["Agent", "Task"]);

/** Tool calls worth a line, and the field that says what the call was actually about. */
const TOOL_DETAIL = {
  Bash: (i) => i.description || i.command,
  Read: (i) => i.file_path,
  Write: (i) => i.file_path,
  Edit: (i) => i.file_path,
  Glob: (i) => i.pattern,
  Grep: (i) => i.pattern,
  // Both names appear depending on the client. Treat them as the same thing everywhere,
  // including in the detection below — looking for only one of them meant a run that
  // delegated eleven times was recorded as having delegated none, and its work was
  // rejected twice for it.
  Agent: (i) => `${i.subagent_type ? `${i.subagent_type}: ` : ""}${i.description ?? ""}`,
  Task: (i) => `${i.subagent_type ? `${i.subagent_type}: ` : ""}${i.description ?? ""}`,
  Skill: (i) => i.skill ?? i.name ?? "",
  WebFetch: (i) => i.url,
};

const truncate = (text, n = 96) => {
  const one = String(text ?? "").replace(/\s+/g, " ").trim();
  return one.length > n ? `${one.slice(0, n - 1)}…` : one;
};

/**
 * Render one stream-json line. Returns the text to print, or null when the event carries
 * nothing a watcher needs.
 *
 * `state` is carried between calls so repeated prose deltas from one message collapse into a
 * single line rather than one line per token.
 */
export function renderEvent(line, state = {}) {
  let event;
  try {
    event = JSON.parse(line);
  } catch {
    return null; // Not a JSON line — a warning on stderr, or a partial write. Ignore it.
  }
  if (!event || typeof event !== "object") return null;

  if (event.type === "result") {
    const cost = typeof event.total_cost_usd === "number" ? ` · $${event.total_cost_usd.toFixed(2)}` : "";
    const turns = typeof event.num_turns === "number" ? ` · ${event.num_turns} turns` : "";
    return event.is_error
      ? `  ✗ ended with an error${turns}${cost}`
      : `  ✓ done${turns}${cost}`;
  }

  if (event.type !== "assistant") return null;

  const content = event.message?.content;
  if (!Array.isArray(content)) return null;

  const out = [];
  for (const block of content) {
    if (block?.type === "text" && block.text?.trim()) {
      const text = truncate(block.text, 140);
      // Collapse a stream of deltas from the same message into one line.
      if (text && text !== state.lastText) {
        state.lastText = text;
        out.push(`  · ${text}`);
      }
    } else if (block?.type === "tool_use") {
      const detail = TOOL_DETAIL[block.name]?.(block.input ?? {});
      out.push(`  → ${block.name}${detail ? `  ${truncate(detail, 84)}` : ""}`);
    }
  }
  return out.length ? out.join("\n") : null;
}

/**
 * Consume a stream-json feed, printing progress and returning what the run cost.
 *
 * Resolves with `{ costUsd, isError, subagents }`. `costUsd` is null when no parseable
 * result event arrived — a caller enforcing a spend ceiling must treat that as unknown,
 * never as zero, or the ceiling is bypassed by exactly the runs most likely to be
 * misbehaving. `subagents` lists every specialist the session delegated to, in order, which
 * is how the runner tells a task that reviewed its work from one that only said it would.
 * `skills` lists the procedures it loaded, checked the same way and for the same reason.
 */
export function renderStream(readable, write = (s) => console.log(s), limits = {}) {
  const { maxTurns = Infinity, onLimit = () => {} } = limits;
  return new Promise((resolve) => {
    const state = {};
    // Turns, counted as they stream. The dollar figure only arrives in the final event, so
    // a run that never finishes never reports a cost — which is how one attempt reached 984
    // turns and $240 against a $50 ceiling that is only checked between tasks. Turns are the
    // one budget observable while a run is still going.
    let turns = 0;
    let stopped = false;
    // Only the parent session's turns count. Every subagent streams through here too, so
    // counting all of them meant a task that delegated to six departments blew the budget
    // on their work rather than its own — punishing exactly the thorough runs the pipeline
    // exists to produce. It killed nine of them. The first session id seen is the parent's.
    let rootSession = null;
    // An account-level refusal — usage limit, expired credentials, a suspended key — is not
    // this task's failure. Every task after it fails the same way, so the caller has to be
    // able to tell "this change is wrong" from "nothing can run right now".
    let blocked = null;
    let buffer = "";
    let costUsd = null;
    let isError = false;
    // Which specialists the session actually delegated to. Instructing a brief to route
    // work is a request; this is the observation that says whether it happened, and it is
    // what lets the runner refuse a task that skipped its review.
    const subagents = [];
    // When each of those delegations was dispatched, in seconds from the start of the
    // attempt. Turns were journalled to answer "is the turn cap too high?"; this answers the
    // question that replaced it — where does a 37-minute task spend 37 minutes? Without it,
    // cutting a reviewer to go faster is a guess, and a guess is how this loop already lost
    // 40% of its landed work once. Names stay in `subagents` unchanged, because the
    // department check reads that array and a shape change there would fail a passing task.
    const startedMs = Date.now();
    const subagentAtSec = [];
    // Skills the session loaded. Same reasoning as the delegations: the brief names which
    // skill fits the work, and this is the observation that says whether it was read.
    const skills = [];

    const consume = (chunk) => {
      buffer += chunk;
      const lines = buffer.split("\n");
      buffer = lines.pop() ?? "";
      for (const line of lines) {
        if (!line.trim()) continue;
        try {
          const parsed = JSON.parse(line);
          if (parsed?.type === "result") {
            if (typeof parsed.total_cost_usd === "number") costUsd = parsed.total_cost_usd;
            isError = Boolean(parsed.is_error);
          }
          if (parsed?.type === "assistant" && !blocked) {
            for (const b of parsed.message?.content ?? []) {
              if (b?.type !== "text" || !b.text) continue;
              if (isRefusal(b.text)) blocked = truncate(b.text, 160);
            }
          }
          if (parsed?.session_id && !rootSession) rootSession = parsed.session_id;
          // An event with no session id at all is the parent's: some clients omit it, and
          // a feed that never identifies a session is a single session by definition.
          const isParent = !parsed?.session_id || parsed.session_id === rootSession;
          if (parsed?.type === "assistant" && isParent) {
            turns += 1;
            if (turns > maxTurns && !stopped) {
              stopped = true;
              write(`  ✗ stopped: ${turns} turns exceeds the per-attempt limit of ${maxTurns}`);
              onLimit({ turns, maxTurns });
            }
          }
          if (parsed?.type === "assistant" && Array.isArray(parsed.message?.content)) {
            for (const block of parsed.message.content) {
              if (
                block?.type === "tool_use" &&
                DELEGATION_TOOLS.has(block.name) &&
                block.input?.subagent_type
              ) {
                subagents.push(String(block.input.subagent_type));
                subagentAtSec.push(Math.round((Date.now() - startedMs) / 1000));
              }
              if (block?.type === "tool_use" && block.name === "Skill") {
                const named = block.input?.skill ?? block.input?.name;
                if (named) skills.push(String(named));
              }
            }
          }
        } catch {
          // Rendered below on a best-effort basis; unparseable lines carry no cost data.
        }
        const rendered = renderEvent(line, state);
        if (rendered) write(rendered);
      }
    };

    readable.setEncoding("utf8");
    readable.on("data", consume);
    readable.on("end", () => {
      if (buffer.trim()) consume("\n");
      resolve({ costUsd, isError, subagents, subagentAtSec, elapsedSec: Math.round((Date.now() - startedMs) / 1000), skills, turns, stoppedAtLimit: stopped, blocked });
    });
    readable.on("error", () => resolve({ costUsd, isError, subagents, skills, turns, stoppedAtLimit: stopped, blocked }));
  });
}
