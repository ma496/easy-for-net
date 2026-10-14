/**
 * The session runner against a fake CLI: a `node` child that replays a stream from stdin and
 * then does whatever the case needs — exit, hang, or leave an orphan holding its output.
 */
import { strict as assert } from "node:assert";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { after, test } from "node:test";
import { fileURLToPath } from "node:url";
import { attemptOutcome } from "../lib/attempt-outcome.mjs";
import { runStreaming } from "../lib/claude-session.mjs";
import { isAlive } from "../lib/proc.mjs";

const FIXTURE = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), "fixtures", "claude-stream", "synthetic", "background-settle.jsonl"),
  "utf8",
);

/**
 * A fake CLI, written to a file rather than passed to `node -e`: on Windows the spawn goes
 * through cmd.exe, which ends a command line at its first newline. It prints its pid, echoes
 * stdin to stdout, and then, by mode, hangs, exits, or exits leaving an orphan on its output.
 */
const FAKE_DIR = mkdtempSync(join(tmpdir(), "claude-session-"));
const FAKE_CLI = join(FAKE_DIR, "fake-claude.cjs");
after(() => rmSync(FAKE_DIR, { recursive: true, force: true }));
writeFileSync(
  FAKE_CLI,
  `const mode = process.argv[2];
console.log("pid " + process.pid);
let input = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", (c) => (input += c));
process.stdin.on("end", () => {
  process.stdout.write(input);
  if (mode === "hang") setInterval(() => {}, 1e6);
  if (mode === "orphan") {
    const { spawn } = require("node:child_process");
    // Detached, or Windows ends it with its parent and nothing is left holding the pipe.
    const o = spawn(process.execPath, ["-e", "setTimeout(() => {}, 120000)"], { stdio: ["ignore", "inherit", "inherit"], detached: true });
    o.unref();
    console.log("orphan " + o.pid);
    process.exit(0);
  }
});
`,
);
const fakeCli = (mode) => [FAKE_CLI, mode];

/** Fast clocks for a test; the real ones are seconds. */
function options(extra = {}) {
  const lines = [];
  const notices = [];
  return {
    lines,
    notices,
    opts: {
      write: () => {},
      log: (s) => notices.push(s),
      onLine: (l) => lines.push(l),
      settleMs: 300,
      checkMs: 50,
      stopGraceMs: 2_000,
      ...extra,
    },
  };
}
const pidIn = (lines) => Number(/^pid (\d+)$/.exec(lines.find((l) => l.startsWith("pid ")) ?? "")?.[1]);

test("a session that finished but does not exit is closed and judged finished", async () => {
  const { lines, notices, opts } = options();
  const started = Date.now();
  const res = await runStreaming(process.execPath, fakeCli("hang"), { ...opts, input: FIXTURE });
  assert.equal(res.settled, true);
  assert.equal(res.timedOut, false);
  assert.equal(res.resultSubtype, "success");
  assert.equal(res.costUsd, 12.299549800000001, "the last result's cost is kept");
  assert.equal(attemptOutcome(res), "finished", "closed by the runner, still a finished attempt");
  assert.ok(Date.now() - started < 20_000, "closed within seconds of going quiet");
  assert.match(notices.join("\n"), /did not exit — closed it after \d+s of quiet/);
  const pid = pidIn(lines);
  assert.ok(pid > 0);
  assert.equal(isAlive(pid), false, "the hung CLI was killed");
});

test("a session still waiting on a background task is not closed early", async () => {
  const stream = [
    { type: "system", subtype: "init", session_id: "s", claude_code_version: "2.1.300" },
    { type: "system", subtype: "background_tasks_changed", session_id: "s", tasks: [{ task_id: "a1", task_type: "local_agent" }] },
    { type: "assistant", parent_tool_use_id: null, session_id: "s", message: { id: "m1", content: [] } },
    { type: "result", subtype: "success", is_error: false, num_turns: 1, total_cost_usd: 1, session_id: "s" },
  ]
    .map((e) => JSON.stringify(e))
    .join("\n");
  const { opts } = options({ attemptEndsAt: Date.now() + 2_500 });
  const res = await runStreaming(process.execPath, fakeCli("hang"), { ...opts, input: `${stream}\n` });
  assert.equal(res.settled, false, "a reviewer was still running");
  assert.equal(res.timedOut, true, "only the deadline ended it");
  assert.equal(res.timeLimit, "attempt");
  assert.equal(attemptOutcome(res), "limit");
});

test("a session that exits on its own is not reported as settled", async () => {
  const { opts } = options();
  const res = await runStreaming(process.execPath, fakeCli("exit"), { ...opts, input: FIXTURE });
  assert.equal(res.status, 0);
  assert.equal(res.settled, false);
  assert.equal(res.resultSubtype, "success");
  assert.equal(attemptOutcome(res), "finished");
});

test("an orphan holding the output does not keep the runner waiting after the CLI exits", async () => {
  const { lines, opts } = options({ settleMs: Infinity, stopGraceMs: 1_000 });
  const started = Date.now();
  const res = await runStreaming(process.execPath, fakeCli("orphan"), { ...opts, input: "" });
  assert.equal(res.status, 0);
  assert.equal(res.settled, false);
  const elapsed = Date.now() - started;
  const orphanPid = Number(/^orphan (\d+)$/.exec(lines.find((l) => l.startsWith("orphan ")) ?? "")?.[1]);
  const orphanHeld = orphanPid > 0 && isAlive(orphanPid);
  if (orphanHeld) process.kill(orphanPid);
  assert.ok(orphanHeld, "the orphan outlived the CLI, holding its output");
  assert.ok(elapsed >= 900, `the grace was what ended the wait (${elapsed}ms)`);
  assert.ok(elapsed < 20_000, "it stopped waiting a grace period after the exit");
});
