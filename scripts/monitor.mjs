#!/usr/bin/env node
/**
 * A read-only web page over the agent queue: every spec's tasks by lane, each task's attempts
 * from the journal, and the live narration of the attempt being built.
 *
 *   npm run monitor                     # http://127.0.0.1:4000
 *   npm run monitor -- --port 4020
 *   npm run monitor -- --runs-dir <dir> # read stream files and drain.log from another folder
 *
 * It only reads — no lock, no lane moves, no model, no writes — so it can run beside a drain.
 * It listens on loopback alone and answers only a loopback Host header: a stream carries the
 * task's brief, file paths and the commands run, which is nobody else's business.
 *
 * The pure parts (what the board holds, what a stream line means) are `lib/monitor.mjs`.
 */
import { closeSync, existsSync, openSync, readFileSync, readSync, readdirSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { hostname } from "node:os";
import { join, resolve } from "node:path";
import { LANES, agentsOf, boardSnapshot, describeEvent, isStreamFileName, newestStream } from "./lib/monitor.mjs";
import { isAlive } from "./lib/proc.mjs";
import { REPO_ROOT } from "./lib/project-config.mjs";
import { isHeld, parseLock } from "./lib/queue-lock.mjs";
import { listTasks } from "./lib/task-names.mjs";
import { LOG_DIR, readRuns } from "./run-journal.mjs";

const HELP = `npm run monitor [-- --port <n>] [--runs-dir <dir>]

A read-only dashboard over .agent-queue/ and .agent-runs/ at http://127.0.0.1:<port>
(default 4000, or MONITOR_PORT). It never changes the queue.`;

const argv = process.argv.slice(2);
if (argv.includes("--help") || argv.includes("-h")) {
  console.log(HELP);
  process.exit(0);
}
const flag = (name) => {
  const i = argv.indexOf(name);
  return i >= 0 ? argv[i + 1] : undefined;
};
const PORT = Number(flag("--port") ?? process.env.MONITOR_PORT ?? 4000);
if (!Number.isInteger(PORT) || PORT <= 0 || PORT > 65535) {
  console.error(`monitor: not a port: ${flag("--port") ?? process.env.MONITOR_PORT}`);
  process.exit(1);
}
const RUNS_DIR = flag("--runs-dir") ? resolve(flag("--runs-dir")) : LOG_DIR;
const QUEUE_DIR = join(REPO_ROOT, ".agent-queue");
const PAGE = join(REPO_ROOT, "scripts", "lib", "monitor-page.html");

/** How many described rows a stream keeps; a feed opened on it starts with all of them. */
const MAX_ROWS = 20_000;
const POLL_MS = 1000;

const readText = (path) => {
  try {
    return readFileSync(path, "utf8");
  } catch {
    return null; // Moved between lanes while we looked, or not written yet.
  }
};

function streamFiles() {
  if (!existsSync(RUNS_DIR)) return [];
  const out = [];
  for (const name of readdirSync(RUNS_DIR)) {
    if (!isStreamFileName(name)) continue;
    try {
      out.push({ name, mtimeMs: statSync(join(RUNS_DIR, name)).mtimeMs });
    } catch {
      // Gone between the listing and the stat.
    }
  }
  return out;
}

function board() {
  const lanes = {};
  for (const lane of LANES) {
    const dir = join(QUEUE_DIR, lane);
    lanes[lane] = listTasks(dir)
      .map((file) => ({ file, text: readText(join(dir, file)) }))
      .filter((t) => t.text !== null);
  }
  const plannedDir = join(QUEUE_DIR, "planned");
  const specs = existsSync(plannedDir)
    ? readdirSync(plannedDir).filter((f) => f.endsWith(".md.json")).map((f) => f.slice(0, -".md.json".length))
    : [];
  const lockPath = join(QUEUE_DIR, "drain.lock");
  const lock = parseLock(readText(lockPath));
  let mtimeMs = null;
  try {
    mtimeMs = statSync(lockPath).mtimeMs;
  } catch {
    // No lock: nothing is draining.
  }
  const lockHeld = isHeld(lock, { host: hostname(), isAlive, mtimeMs });
  const live = newestStream(streamFiles());
  let liveAgents = [];
  let liveState = {};
  if (live) {
    const t = trackerFor(live.name);
    advance(t);
    liveAgents = agentsOf(t.state);
    liveState = t.state;
  }
  return boardSnapshot({ lanes, specs, runs: readRuns(), lock, lockHeld, live, liveAgents, liveState });
}

/** The last `count` lines of drain.log, read from its tail rather than whole. */
function drainLogTail(count) {
  const path = join(RUNS_DIR, "drain.log");
  if (!existsSync(path)) return [];
  const size = statSync(path).size;
  const want = Math.min(size, 256 * 1024);
  const buf = Buffer.alloc(want);
  const fd = openSync(path, "r");
  try {
    readSync(fd, buf, 0, want, size - want);
  } finally {
    closeSync(fd);
  }
  const lines = buf.toString("utf8").split(/\r?\n/);
  if (want < size) lines.shift(); // Probably cut mid-line.
  while (lines.length && !lines.at(-1)) lines.pop();
  return lines.slice(-count);
}

/** Bytes `from`..end of a file, or null when it cannot be read. */
function readFrom(path, from) {
  try {
    const size = statSync(path).size;
    if (size < from) return { size, text: null }; // Replaced or truncated: start again.
    if (size === from) return { size, text: "" };
    const buf = Buffer.alloc(size - from);
    const fd = openSync(path, "r");
    try {
      readSync(fd, buf, 0, buf.length, from);
    } finally {
      closeSync(fd);
    }
    return { size, text: buf.toString("utf8") };
  } catch {
    return null;
  }
}

/**
 * One incremental reading of one stream file, shared by the board and every feed open on it,
 * so a file is parsed once however many tabs watch it. Polled rather than watched: fs.watch on
 * Windows misses appends made by another process often enough to look like a hung run.
 */
const trackers = new Map();

function trackerFor(name) {
  let t = trackers.get(name);
  if (!t) {
    t = { name, offset: 0, carry: "", state: {}, rows: [], dropped: 0, counts: "", listeners: new Set(), usedAt: 0 };
    trackers.set(name, t);
    advance(t);
  }
  t.usedAt = Date.now();
  return t;
}

const trackerCounts = (t) => ({ turns: t.state.turns ?? 0, subagentTurns: t.state.subagentTurns ?? 0, agents: agentsOf(t.state) });

function advance(t) {
  const read = readFrom(join(RUNS_DIR, t.name), t.offset);
  if (!read) return;
  if (read.text === null) {
    // Rewritten from the start: read it again, and tell every feed to start over.
    Object.assign(t, { offset: 0, carry: "", state: {}, rows: [], dropped: 0, counts: "" });
    for (const l of t.listeners) l.reset();
    advance(t);
    return;
  }
  t.offset = read.size;
  const lines = (t.carry + read.text).split("\n");
  t.carry = lines.pop() ?? ""; // A line the runner has not finished writing.
  const rows = lines.flatMap((line) => describeEvent(line, t.state));
  if (!lines.length) return;
  // Lines that add no row can still move an agent (a status patch, its usage), so when the
  // counts changed every feed hears of it: an empty `rows` event carries the new ones.
  const counts = JSON.stringify(trackerCounts(t));
  const moved = counts !== t.counts;
  t.counts = counts;
  if (!rows.length) {
    if (moved) for (const l of t.listeners) l.rows([]);
    return;
  }
  t.rows.push(...rows);
  if (t.rows.length > MAX_ROWS) {
    t.dropped += t.rows.length - MAX_ROWS;
    t.rows.splice(0, t.rows.length - MAX_ROWS);
  }
  for (const l of t.listeners) l.rows(rows);
}

setInterval(() => {
  const now = Date.now();
  for (const t of trackers.values()) {
    if (t.listeners.size) advance(t);
    // The board touches the live file every few seconds; anything else idle is let go.
    else if (now - t.usedAt > 5 * 60_000) trackers.delete(t.name);
  }
}, POLL_MS).unref();

/** Server-Sent Events over a tracker. `file=live` follows whichever stream is newest. */
function serveStream(req, res, requested) {
  const following = requested === "live";
  if (!following && !isStreamFileName(requested)) {
    res.writeHead(400, { "content-type": "text/plain" }).end("not a stream file");
    return;
  }
  res.writeHead(200, {
    "content-type": "text/event-stream",
    "cache-control": "no-cache",
    connection: "keep-alive",
  });
  const send = (event, data) => res.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);

  let current = null;
  const listener = {
    rows: (rows) => send("rows", { rows, ...trackerCounts(current) }),
    reset: () => send("backlog", { file: current.name, following, rows: [], skipped: 0, ...trackerCounts(current) }),
  };
  const attach = (name) => {
    if (current) current.listeners.delete(listener);
    current = trackerFor(name);
    current.listeners.add(listener);
    send("backlog", { file: name, following, rows: current.rows, skipped: current.dropped, ...trackerCounts(current) });
  };

  const check = () => {
    if (following) {
      const newest = newestStream(streamFiles());
      if (newest && newest.name !== current?.name) attach(newest.name);
    } else if (!current) {
      if (existsSync(join(RUNS_DIR, requested))) attach(requested);
      else send("missing", { file: requested });
    }
  };
  check();
  const timer = following ? setInterval(check, POLL_MS) : null;
  const ping = setInterval(() => res.write(": ping\n\n"), 15_000);
  req.on("close", () => {
    clearInterval(timer);
    clearInterval(ping);
    current?.listeners.delete(listener);
  });
}

/** Only a loopback Host: a page on another site cannot rebind a name to us and read the feed. */
const LOOPBACK_HOST = new RegExp(`^(127\\.0\\.0\\.1|localhost|\\[::1\\]):${PORT}$`, "i");

const server = createServer((req, res) => {
  if (!LOOPBACK_HOST.test(req.headers.host ?? "")) {
    res.writeHead(403, { "content-type": "text/plain" }).end("loopback only");
    return;
  }
  if (req.method !== "GET") {
    res.writeHead(405, { allow: "GET" }).end();
    return;
  }
  const url = new URL(req.url ?? "/", `http://${req.headers.host}`);
  const json = (value) => {
    res.writeHead(200, { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" });
    res.end(JSON.stringify(value));
  };
  try {
    if (url.pathname === "/") {
      res.writeHead(200, { "content-type": "text/html; charset=utf-8", "cache-control": "no-store" });
      res.end(readFileSync(PAGE, "utf8"));
    } else if (url.pathname === "/api/board") {
      json(board());
    } else if (url.pathname === "/api/stream") {
      serveStream(req, res, url.searchParams.get("file") ?? "live");
    } else if (url.pathname === "/api/drain-log") {
      const n = Math.min(2000, Math.max(1, Number(url.searchParams.get("lines")) || 200));
      json({ lines: drainLogTail(n) });
    } else {
      res.writeHead(404, { "content-type": "text/plain" }).end("not found");
    }
  } catch (err) {
    if (!res.headersSent) res.writeHead(500, { "content-type": "text/plain" });
    res.end(String(err?.message ?? err));
  }
});

server.on("error", (err) => {
  console.error(err.code === "EADDRINUSE" ? `monitor: port ${PORT} is in use — try --port <n>` : `monitor: ${err.message}`);
  process.exit(1);
});
server.listen(PORT, "127.0.0.1", () => {
  console.log(`Queue monitor: http://127.0.0.1:${PORT}  (read-only · Ctrl+C to stop)`);
  console.log(`  queue: ${QUEUE_DIR}`);
  console.log(`  runs:  ${RUNS_DIR}`);
});
