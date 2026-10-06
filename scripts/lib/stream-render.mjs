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
 *
 * What the events mean is `claude-events.mjs`'s business; this file decides what to print
 * and what to add up.
 */
import {
  DELEGATION_TOOLS,
  LIMIT_SUBTYPES,
  SKILL_TOOL,
  isSubagentEvent,
  normalizeEvent,
  parseLine,
  skillNameOf,
} from "./claude-events.mjs";

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
  // A refused connection to this machine is the app under test being down, which is the
  // task's problem to fix — not the API being out of reach.
  const local = /\blocalhost\b|\b127\.0\.0\.1\b|\[::1\]|\b0\.0\.0\.0\b/i.test(t);
  return (NETWORK_REFUSAL.test(t) && !local) || ENTITLEMENT_REFUSAL.test(withoutCode(t));
}

/** Re-exported so callers that only render do not need to know the adapter exists. */
export { DELEGATION_TOOLS };

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
  Skill: (i) => skillNameOf(i) ?? "",
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
 * single line rather than one line per token. A subagent's lines are indented under a bar,
 * so a watcher can tell the lead's work from a specialist's.
 */
export function renderEvent(line, state = {}) {
  const event = normalizeEvent(parseLine(line));
  if (event.kind === "result") {
    const { costUsd, numTurns, isError, subtype } = event.result;
    const cost = typeof costUsd === "number" ? ` · $${costUsd.toFixed(2)}` : "";
    const turns = typeof numTurns === "number" ? ` · ${numTurns} turns` : "";
    const why = subtype && subtype !== "success" ? ` (${subtype})` : "";
    return isError ? `  ✗ ended with an error${why}${turns}${cost}` : `  ✓ done${turns}${cost}`;
  }
  if (event.kind !== "assistant") return null;

  if (event.sessionId && !state.rootSession) state.rootSession = event.sessionId;
  const lead = isSubagentEvent(event, state.rootSession) ? "  │ " : "  ";
  const out = [];
  for (const text of event.texts) {
    const one = truncate(text, 140);
    // Collapse a stream of deltas from the same message into one line.
    if (one && one !== state.lastText) {
      state.lastText = one;
      out.push(`${lead}· ${one}`);
    }
  }
  for (const use of event.toolUses) {
    const detail = TOOL_DETAIL[use.name]?.(use.input);
    out.push(`${lead}→ ${use.name}${detail ? `  ${truncate(detail, 84)}` : ""}`);
  }
  return out.length ? out.join("\n") : null;
}

/**
 * Consume a stream-json feed, printing progress and returning what the run cost.
 *
 * `costUsd` is null when no parseable result event arrived — a caller enforcing a spend
 * ceiling must treat that as unknown, never as zero, or the ceiling is bypassed by exactly
 * the runs most likely to be misbehaving. `subagents` lists every specialist the *session
 * itself* delegated to, in order, which is how the runner tells a task that reviewed its
 * work from one that only said it would. `skills` lists the procedures loaded anywhere in
 * the run — a builder loading the endpoint guide is the procedure being read, whoever read it.
 *
 * `limits.maxTurns` is a backstop. The runner passes `--max-turns` to the CLI, which stops
 * the session itself and still reports what it cost; this only kills a session that went
 * past it anyway. `limits.onLine` sees every raw line, which is how the runner keeps the
 * stream on disk.
 */
export function renderStream(readable, write = (s) => console.log(s), limits = {}) {
  const { maxTurns = Infinity, onLimit = () => {}, onLine = () => {} } = limits;
  return new Promise((resolve) => {
    const state = {};
    // Parent turns, counted as they stream. The dollar figure only arrives in the final
    // event, so a run that never finishes never reports a cost — which is how one attempt
    // reached 984 turns and $240 against a $50 ceiling. Turns are observable while the run
    // is still going.
    //
    // A turn is one API message, and one message arrives as one event per content block,
    // all with the same `message.id` — so turns are counted by distinct id. An event with
    // no id at all (an older client, a hand-written feed) counts once by itself.
    const parentMessages = new Set();
    let anonymousTurns = 0;
    // Subagents' turns, kept apart. Counting them against the parent killed nine runs that
    // were delegating exactly as instructed.
    const subagentMessages = new Set();
    let anonymousSubagentTurns = 0;
    let stopped = false;
    // An account-level refusal — usage limit, expired credentials, a suspended key — is not
    // this task's failure. Every task after it fails the same way, so the caller has to be
    // able to tell "this change is wrong" from "nothing can run right now".
    let blocked = null;
    let buffer = "";
    let costUsd = null;
    let isError = false;
    let resultSubtype = null;
    let resultTurns = null;
    let cliVersion = null;
    // Which specialists the session actually delegated to, and when (seconds from the start
    // of the attempt). Only the session's own calls: a specialist that spawned a helper has
    // not put the change in front of another department. Names stay in `subagents`
    // unchanged, because the department check reads that array.
    const subagents = [];
    const subagentAtSec = [];
    const skills = [];
    // Each call is counted once, by its own id, however many events repeat its block.
    const seenCalls = new Set();
    const startedMs = Date.now();

    const turns = () => parentMessages.size + anonymousTurns;

    const consumeLine = (line) => {
      if (!line.trim()) return;
      onLine(line);
      const raw = parseLine(line);
      if (raw) {
        const event = normalizeEvent(raw);
        if (event.sessionId && !state.rootSession) state.rootSession = event.sessionId;
        if (event.kind === "system" && event.subtype === "init" && typeof raw.claude_code_version === "string") {
          cliVersion = raw.claude_code_version;
        }
        if (event.kind === "result") {
          // Cumulative, and there can be several (a background subagent reporting after the
          // first answer): the last one is the whole run.
          if (event.result.costUsd !== null) costUsd = event.result.costUsd;
          isError = event.result.isError;
          resultSubtype = event.result.subtype;
          resultTurns = event.result.numTurns;
          if (!blocked && event.result.isError && isRefusal(event.result.text)) {
            blocked = truncate(event.result.text, 160);
          }
        }
        if (event.kind === "assistant") {
          const sub = isSubagentEvent(event, state.rootSession);
          // The parent's own messages only. A subagent reports what it saw — "the smoke check
          // failed: ECONNREFUSED" is a finding about the app, not the account refusing — and
          // a refusal that really stops a subagent reaches the parent's stream as well.
          if (!blocked && !sub) {
            const refusal = event.texts.find((t) => isRefusal(t));
            if (refusal) blocked = truncate(refusal, 160);
          }
          if (sub) {
            if (event.messageId) subagentMessages.add(event.messageId);
            else anonymousSubagentTurns += 1;
          } else {
            if (event.messageId) parentMessages.add(event.messageId);
            else anonymousTurns += 1;
            if (turns() > maxTurns && !stopped) {
              stopped = true;
              write(`  ✗ stopped: ${turns()} turns exceeds the per-attempt limit of ${maxTurns}`);
              onLimit({ turns: turns(), maxTurns });
            }
          }
          for (const use of event.toolUses) {
            if (use.id) {
              if (seenCalls.has(use.id)) continue;
              seenCalls.add(use.id);
            }
            if (!sub && DELEGATION_TOOLS.has(use.name) && use.input.subagent_type) {
              subagents.push(String(use.input.subagent_type));
              subagentAtSec.push(Math.round((Date.now() - startedMs) / 1000));
            }
            if (use.name === SKILL_TOOL) {
              const named = skillNameOf(use.input);
              if (named) skills.push(named);
            }
          }
        }
      }
      const rendered = renderEvent(line, state);
      if (rendered) write(rendered);
    };

    const consume = (chunk) => {
      buffer += chunk;
      const lines = buffer.split("\n");
      buffer = lines.pop() ?? "";
      for (const line of lines) consumeLine(line);
    };

    // One shape on every path, so a field added here never reads as undefined at the caller.
    const outcome = () => ({
      costUsd,
      isError,
      resultSubtype,
      resultTurns,
      stoppedByCli: LIMIT_SUBTYPES.has(resultSubtype),
      cliVersion,
      subagents,
      subagentAtSec,
      elapsedSec: Math.round((Date.now() - startedMs) / 1000),
      skills,
      turns: turns(),
      subagentTurns: subagentMessages.size + anonymousSubagentTurns,
      stoppedAtLimit: stopped,
      blocked,
    });

    readable.setEncoding("utf8");
    readable.on("data", consume);
    readable.on("end", () => {
      if (buffer.trim()) consume("\n");
      resolve(outcome());
    });
    readable.on("error", () => resolve(outcome()));
  });
}
