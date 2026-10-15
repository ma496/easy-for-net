/**
 * The pure half of `monitor.mjs`: what the queue looks like right now, and what one line of a
 * session's stream means to a person watching it.
 *
 * The monitor only reads. It never takes the queue lock, never moves a task and never writes,
 * so it is safe beside a live drain — which is the whole point of it. Everything here takes
 * its inputs as arguments; the entry script gathers them from disk.
 *
 * What a stream line means is still `claude-events.mjs`'s business. This file decides what a
 * browser is shown, and leaves out what it must not be: the model's private reasoning and the
 * contents a tool returned.
 */
import {
  DELEGATION_TOOLS,
  SKILL_TOOL,
  backgroundTaskOf,
  isSubagentEvent,
  normalizeEvent,
  parseLine,
  skillNameOf,
} from "./claude-events.mjs";
import { isBookkeeping, isLanded, markOf } from "./outcomes.mjs";
import { toolDetail } from "./stream-render.mjs";
import { nameOf, qualify, scopeOf, stemOf } from "./task-names.mjs";

/** The lanes, in the order a task moves through them. */
export const LANES = ["todo", "doing", "done", "failed"];

/**
 * Which lane wins when a task's file is in two at once. The runner copies a task into doing/
 * while its todo/ file is still tracked, so "in todo and doing" means "being built".
 */
const LANE_PRECEDENCE = { doing: 4, failed: 3, done: 2, todo: 1 };

/** A stream file the monitor may open: a run's attempt, by name alone. */
export const isStreamFileName = (name) =>
  typeof name === "string" && /^run-[\w-]+-attempt-\d+\.stream\.jsonl$/.test(name);

/** The run id a stream file belongs to, or null. */
export const runIdOfStream = (name) => (isStreamFileName(name) ? name.replace(/-attempt-\d+\.stream\.jsonl$/, "") : null);

/** Which attempt of its run a stream file is, or null. */
export const attemptOfStream = (name) => (isStreamFileName(name) ? Number(/-attempt-(\d+)\.stream\.jsonl$/.exec(name)[1]) : null);

/** When a run started, read from its id (`run-2026-10-14T23-03-40-540Z`), or null. */
export function runStartOf(runId) {
  const m = /^run-(\d{4}-\d{2}-\d{2})T(\d{2})-(\d{2})-(\d{2})-(\d{3})Z$/.exec(String(runId ?? ""));
  return m ? `${m[1]}T${m[2]}:${m[3]}:${m[4]}.${m[5]}Z` : null;
}

/** The line `agent-run.mjs` writes into an attempt's stream for each of its own steps. */
export const RUNNER_PHASE = "runner_phase";

const isoOrNull = (v) => (typeof v === "string" && Number.isFinite(Date.parse(v)) ? v : null);

/** The newest of `[{ name, mtimeMs }]` that is a stream file, or null. */
export function newestStream(entries) {
  let best = null;
  for (const e of entries ?? []) {
    if (!isStreamFileName(e?.name)) continue;
    if (!best || e.mtimeMs > best.mtimeMs) best = e;
  }
  return best;
}

const clip = (text, n) => {
  const one = String(text ?? "").replace(/\s+/g, " ").trim();
  return one.length > n ? `${one.slice(0, n - 1)}…` : one;
};

/** Like `clip`, but keeps the line breaks prose and reports are written with. */
const clipProse = (text, n) => {
  const kept = String(text ?? "").replace(/\r\n?/g, "\n").replace(/\n{3,}/g, "\n\n").trim();
  return kept.length > n ? `${kept.slice(0, n - 1)}…` : kept;
};

/** A task file's title: its first non-empty line, without a heading marker. */
export function titleOf(text) {
  const line = String(text ?? "").split("\n").find((l) => l.trim());
  return line ? line.replace(/^#+\s*/, "").trim() : "";
}

/** The stems a task's `Depends-on:` line names — the same rule `agent-queue.mjs` drains by. */
export function dependencyNames(text, stem) {
  const head = String(text ?? "").split("\n").slice(0, 12);
  const line = head.find((l) => /^depends-on\s*:/i.test(l.trim()));
  if (!line) return [];
  return line
    .split(":")
    .slice(1)
    .join(":")
    .split(",")
    .map((d) => d.trim().replace(/\.md$/, ""))
    .filter((d) => d && !/^none$/i.test(d))
    .map((d) => qualify(d, stem));
}

/** How a task notification's status reads on an agent. */
const ENDED = { completed: "done", failed: "failed", killed: "stopped", stopped: "stopped" };

function newAgent(state, use, spawnedBy, at) {
  const type = use.input.subagent_type ? String(use.input.subagent_type) : "general-purpose";
  state.agentCount[type] = (state.agentCount[type] ?? 0) + 1;
  const n = state.agentCount[type];
  const agent = {
    id: use.id,
    type,
    // A second reviewer of the same kind is a different run with a different log.
    label: n > 1 ? `${type} #${n}` : type,
    description: clip(use.input.description, 200),
    parent: spawnedBy,
    background: use.input.run_in_background === true,
    status: "running",
    toolUses: 0,
    tokens: null,
    durationMs: null,
    turns: 0,
    streamed: 0,
    // Whether the CLI reports this agent's lifecycle (task_* events); when it does, only those end it.
    tracked: false,
    current: "",
    hasReport: false,
    // How many times the lead sent it back to work after it reported, and the run it is on now.
    resumes: 0,
    runId: null,
    // What the runs before this one used: each run's usage counts from zero.
    priorToolUses: 0,
    priorDurationMs: 0,
    startedAt: at ?? null,
    startSeq: state.seq,
  };
  if (use.id) state.agents[use.id] = agent;
  return agent;
}

/** The agents a stream has started, in the order it started them, as plain data. */
export function agentsOf(state) {
  return Object.values(state?.agents ?? {})
    .sort((a, b) => a.startSeq - b.startSeq)
    .map((a) => ({ ...a }));
}

/**
 * Describe one stream line as rows for the live feed. `state` is carried across calls: it
 * holds the root session, every agent the stream has started (`agentsOf(state)` reads them),
 * the calls already shown, the turns counted so far (`state.turns`, `state.subagentTurns`)
 * and the sequence number the next row gets.
 *
 * Row: `{ seq, agent, who, sub, kind, text, at?, detail?, ok?, costUsd?, turns? }` — `agent` is
 * the id of the `Agent` call whose log the row belongs to (null for the lead), `who` its label,
 * `kind` one of text | tool | delegate | resume | skill | task | progress | report | result |
 * phase. A delegate row belongs to the agent that delegated, and names the new one in `spawns`.
 * `at` is when the line was written, present only where the line says so (an assistant event,
 * a runner phase): a row is never given a guessed time.
 *
 * A phase row is one of the runner's own steps (`Attempt 1 — verifying`), which it writes into
 * the stream before and after the session. `state.phase` holds the latest, and `state.lastLine`
 * is "runner" while the runner's line is the newest in the file: the session is not running.
 */
export function describeEvent(line, state) {
  const raw = parseLine(line);
  if (!raw) return [];
  state.agents ??= {};
  state.agentCount ??= {};
  state.agentMessages ??= {};
  state.taskAgent ??= {};
  state.taskType ??= {};
  state.seen ??= new Set();
  state.leadMessages ??= new Set();
  state.subMessages ??= new Set();
  state.lastText ??= {};
  state.seq ??= 0;
  const rows = [];
  const push = (row) => {
    state.seq += 1;
    rows.push({ seq: state.seq, agent: null, who: "lead", sub: false, ...row });
  };

  if (raw.type === RUNNER_PHASE) {
    const at = isoOrNull(raw.at);
    const text = clip(raw.text, 300);
    if (!text) return rows;
    state.phase = { text, at };
    state.lastLine = "runner";
    state.firstAt ??= at;
    push({ kind: "phase", text, ...(at ? { at } : {}) });
    return rows;
  }
  state.lastLine = "cli";

  const task = backgroundTaskOf(raw);
  if (task) {
    const agent = state.agents[task.toolUseId] ?? state.agents[state.taskAgent[task.id]];
    if (agent) {
      if (task.id) state.taskAgent[task.id] = agent.id;
      agent.tracked = true;
      const mine = { agent: agent.id, who: agent.label, sub: true };
      // An event of a run the agent has since been resumed out of says nothing about it now.
      const stale = Boolean(task.runId && agent.runId && task.runId !== agent.runId);
      if (task.status === "started") {
        // The same task starting again is the agent resumed (a SendMessage after the reviews):
        // it is working once more, until its next notification. A new run id says so even when
        // the start overtakes the previous run's notification; an older CLI has only the status.
        const resumed = task.runId && agent.runId ? stale : agent.status !== "running";
        if (task.runId) agent.runId = task.runId;
        if (resumed) {
          agent.status = "running";
          agent.current = "";
          agent.hasReport = false;
          agent.resumes += 1;
          agent.priorToolUses = agent.toolUses;
          agent.priorDurationMs = agent.durationMs ?? 0;
          push({ ...mine, kind: "resume", text: "resumed", detail: clip(task.prompt, 1000) });
        }
      } else if (stale) {
        // The earlier run's report still belongs in the log; its status does not.
        if (task.status !== "progress" && task.status !== "updated" && task.description) {
          push({ ...mine, kind: "report", ok: ENDED[task.status] === "done", text: clipProse(task.description, 8000) });
        }
      } else if (task.status === "progress") {
        if (task.usage?.toolUses != null) agent.toolUses = agent.priorToolUses + task.usage.toolUses;
        if (task.usage?.tokens != null) agent.tokens = task.usage.tokens;
        if (task.usage?.durationMs != null) agent.durationMs = agent.priorDurationMs + task.usage.durationMs;
        const now = clip(task.description, 300);
        // The first progress event often repeats the agent's own description; show what changed.
        if (now && now !== agent.current && now !== agent.description) {
          push({ ...mine, kind: "progress", text: now, detail: task.lastTool ?? "" });
        }
        if (now) agent.current = now;
      } else if (task.status === "updated") {
        if (agent.status === "running" && ENDED[task.description]) agent.status = ENDED[task.description];
      } else {
        agent.status = ENDED[task.status] ?? "done";
        agent.current = "";
        agent.hasReport = Boolean(task.description);
        push({ ...mine, kind: "report", ok: agent.status === "done", text: clipProse(task.description, 8000) });
      }
      return rows;
    }
    // A backgrounded shell command: the lead's own work, shown as it starts and ends.
    if (task.status === "progress" || task.status === "updated") return rows;
    if (task.id && task.type) state.taskType[task.id] = task.type;
    push({ kind: "task", text: `background ${task.status}`, detail: clip(task.description, 1000) });
    return rows;
  }

  const event = normalizeEvent(raw);
  if (event.sessionId && !state.rootSession) state.rootSession = event.sessionId;
  if (event.at) state.firstAt ??= event.at;

  if (event.kind === "result") {
    const { costUsd, numTurns, isError, subtype } = event.result;
    // An older CLI reports no task events, so the session ending is the only sign its agents
    // ended. A newer one can answer before an async agent finishes; that agent's own
    // notification ends it.
    for (const a of Object.values(state.agents)) if (a.status === "running" && !a.tracked && !a.background) a.status = "done";
    push({
      kind: "result",
      ok: !isError,
      text: isError ? `ended with an error${subtype && subtype !== "success" ? ` (${subtype})` : ""}` : "done",
      costUsd,
      turns: numTurns,
      detail: isError ? clip(event.result.text, 1000) : "",
    });
    return rows;
  }
  if (event.kind !== "assistant") return rows;

  const sub = isSubagentEvent(event, state.rootSession);
  const agent = sub ? state.agents[event.parentToolUseId] ?? null : null;
  if (event.messageId) {
    (sub ? state.subMessages : state.leadMessages).add(event.messageId);
    if (agent) {
      state.agentMessages[agent.id] ??= new Set();
      state.agentMessages[agent.id].add(event.messageId);
      agent.turns = state.agentMessages[agent.id].size;
    }
  }
  state.turns = state.leadMessages.size;
  state.subagentTurns = state.subMessages.size;
  const mine = { agent: agent?.id ?? null, who: agent?.label ?? (sub ? "subagent" : "lead"), sub, ...(event.at ? { at: event.at } : {}) };

  for (const text of event.texts) {
    const one = clipProse(text, 4000);
    const key = mine.agent ?? (sub ? "subagent" : "lead");
    if (!one || state.lastText[key] === one) continue;
    state.lastText[key] = one;
    if (agent) agent.streamed += 1;
    push({ ...mine, kind: "text", text: one });
  }
  for (const use of event.toolUses) {
    if (use.id) {
      if (state.seen.has(use.id)) continue;
      state.seen.add(use.id);
    }
    if (agent) agent.streamed += 1;
    if (DELEGATION_TOOLS.has(use.name)) {
      const spawned = newAgent(state, use, mine.agent, event.at);
      push({ ...mine, kind: "delegate", text: spawned.label, detail: spawned.description, spawns: spawned.id });
    } else if (use.name === SKILL_TOOL) {
      push({ ...mine, kind: "skill", text: skillNameOf(use.input) ?? "skill" });
    } else {
      push({ ...mine, kind: "tool", text: use.name, detail: clip(toolDetail(use), 1000) });
    }
  }
  return rows;
}


/** What a journal entry says about the task it ran, in the shape the page shows. */
function runSummary(entry) {
  const delegations = (entry.phases ?? []).flatMap((p) => (p?.delegations ?? []).map((d) => d?.name).filter(Boolean));
  const span = Date.parse(entry.finishedAt) - Date.parse(entry.startedAt);
  return {
    id: entry.id,
    startedAt: entry.startedAt ?? null,
    finishedAt: entry.finishedAt ?? null,
    durationMs: Number.isFinite(span) && span >= 0 ? span : null,
    outcome: entry.outcome ?? null,
    mark: markOf(entry.outcome),
    failure: entry.failure ? clip(entry.failure, 600) : null,
    costUsd: typeof entry.costUsd === "number" ? entry.costUsd : null,
    attempts: Number(entry.attempts) || (entry.attemptCostsUsd?.length ?? 1),
    turns: Array.isArray(entry.turns) ? entry.turns : [],
    delegations,
  };
}

/**
 * The board: every spec, its tasks by lane, each with its journal history.
 *
 * `lanes` is `{ todo: [{ file, text }], … }` with `file` as `listTasks` names it; `specs` the
 * planned spec names; `runs` the journal; `lock` the parsed drain lock and `lockHeld` whether
 * `isHeld` judged it live; `live` the newest stream file `{ name, mtimeMs }`, `liveAgents`
 * what `agentsOf` says that stream has started, and `liveState` the rest `describeEvent`
 * gathered from it (`phase`, `lastLine`, `firstAt`).
 */
export function boardSnapshot({ lanes = {}, specs = [], runs = [], lock = null, lockHeld = false, live = null, liveAgents = [], liveState = {}, now = Date.now() } = {}) {
  const tasks = new Map();
  for (const lane of LANES) {
    for (const { file, text } of lanes[lane] ?? []) {
      const stem = stemOf(file);
      const prior = tasks.get(stem);
      if (prior && LANE_PRECEDENCE[prior.lane] >= LANE_PRECEDENCE[lane]) continue;
      tasks.set(stem, {
        stem,
        spec: scopeOf(stem) || "(unscoped)",
        name: nameOf(stem),
        title: titleOf(text),
        lane,
        deps: dependencyNames(text, stem),
      });
    }
  }

  const attempts = runs.filter((e) => e && !isBookkeeping(e));
  const byStem = new Map();
  for (const e of attempts) {
    let stem = e.taskSlug ?? null;
    // Older entries carry no slug; their brief's first line is the task's title.
    if (!stem) {
      const title = titleOf(e.task);
      stem = [...tasks.values()].find((t) => t.title && t.title === title)?.stem ?? null;
    }
    if (!stem) continue;
    if (!byStem.has(stem)) byStem.set(stem, []);
    byStem.get(stem).push(e);
  }

  const landed = new Set([...tasks.values()].filter((t) => t.lane === "done").map((t) => t.stem));
  const specNames = new Set(specs);
  for (const t of tasks.values()) specNames.add(t.spec);

  const bySpec = new Map([...specNames].map((s) => [s, []]));
  for (const t of tasks.values()) {
    const history = (byStem.get(t.stem) ?? []).map(runSummary);
    const last = history.at(-1) ?? null;
    bySpec.get(t.spec).push({
      ...t,
      deps: t.deps.map((d) => ({ stem: d, landed: landed.has(d) })),
      runs: history,
      attempts: history.reduce((n, r) => n + r.attempts, 0),
      costUsd: history.reduce((n, r) => n + (r.costUsd ?? 0), 0),
      lastOutcome: last?.outcome ?? null,
      mark: last ? last.mark : null,
      landedRun: history.some((r) => isLanded(r.outcome)),
    });
  }

  const specsOut = [...bySpec.entries()].map(([name, list]) => {
    list.sort((a, b) => a.name.localeCompare(b.name));
    const counts = Object.fromEntries(LANES.map((l) => [l, list.filter((t) => t.lane === l).length]));
    return { name, tasks: list, counts, costUsd: list.reduce((n, t) => n + t.costUsd, 0) };
  });
  // Work in flight first, then work waiting, then what is finished.
  const rank = (s) => (s.counts.doing ? 0 : s.counts.todo || s.counts.failed ? 1 : 2);
  specsOut.sort((a, b) => rank(a) - rank(b) || a.name.localeCompare(b.name));

  const liveRunId = live ? runIdOfStream(live.name) : null;
  const liveEntry = liveRunId ? attempts.find((e) => e.id === liveRunId) : null;
  const doing = [...tasks.values()].filter((t) => t.lane === "doing");
  return {
    now: new Date(now).toISOString(),
    drain: lock
      ? { state: lockHeld ? "running" : "stale", pid: lock.pid, host: lock.host ?? null, startedAt: lock.startedAt ?? null, heartbeatAt: lock.heartbeatAt ?? null }
      : { state: "idle" },
    live: live
      ? {
          file: live.name,
          updatedAt: new Date(live.mtimeMs).toISOString(),
          // A finished run is in the journal; until then the stream belongs to the task in doing/.
          finished: Boolean(liveEntry),
          stem: liveEntry?.taskSlug ?? (doing.length === 1 ? doing[0].stem : null),
          agents: liveAgents,
          runStartedAt: runStartOf(liveRunId),
          attempt: attemptOfStream(live.name),
          attemptStartedAt: liveState.firstAt ?? null,
          // The runner's latest step, and whether the runner rather than the session is at work
          // now: before the session starts, and while it verifies and commits after it.
          phase: liveState.phase ?? null,
          runnerActive: liveState.lastLine === "runner",
        }
      : null,
    totals: {
      ...Object.fromEntries(LANES.map((l) => [l, specsOut.reduce((n, s) => n + s.counts[l], 0)])),
      costUsd: attempts.reduce((n, e) => n + (typeof e.costUsd === "number" ? e.costUsd : 0), 0),
    },
    specs: specsOut,
  };
}
