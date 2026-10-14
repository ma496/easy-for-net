import { strict as assert } from "node:assert";
import { test } from "node:test";
import { descendantsOf, isAlive, runWithDeadline } from "../lib/proc.mjs";

const node = (script) => ["-e", script];

test("descendants are found through every generation, and only below the root", () => {
  const table = [
    [10, 1],
    [11, 10],
    [12, 11],
    [13, 10],
    [20, 1],
  ];
  assert.deepEqual(descendantsOf(table, 10), [11, 13, 12]);
  assert.deepEqual(descendantsOf(table, 20), []);
});

test("a table that loops back on itself is walked once", () => {
  assert.deepEqual(descendantsOf([[2, 1], [1, 2]], 1), [2]);
});

test("a command that finishes reports its exit code and its output", async () => {
  const res = await runWithDeadline(process.execPath, node("console.log('hi'); process.exit(3)"), { timeoutMs: 30_000 });
  assert.equal(res.status, 3);
  assert.equal(res.timedOut, false);
  assert.match(res.stdout, /hi/);
});

test("a command past its deadline is stopped and reported as timed out", async () => {
  const started = Date.now();
  const res = await runWithDeadline(process.execPath, node("setTimeout(() => {}, 120000)"), {
    timeoutMs: 500,
    graceMs: 2_000,
  });
  assert.equal(res.timedOut, true);
  assert.notEqual(res.status, 0);
  assert.ok(Date.now() - started < 20_000, "it stopped long before the child's own two minutes");
});

test("an orphan holding the pipes does not keep the caller waiting", async () => {
  // The parent starts a grandchild that inherits stdout and outlives it, then exits at once:
  // exactly the background shell that held a stopped attempt open.
  const script = `
    const { spawn } = require("node:child_process");
    const orphan = spawn(process.execPath, ["-e", "setTimeout(() => {}, 120000)"], { stdio: ["ignore", "inherit", "inherit"] });
    console.log("orphan " + orphan.pid);
    process.exit(0);
  `;
  const started = Date.now();
  const res = await runWithDeadline(process.execPath, node(script), { timeoutMs: 60_000, graceMs: 1_000 });
  assert.equal(res.status, 0);
  assert.equal(res.timedOut, false);
  assert.ok(Date.now() - started < 20_000, "it returned a grace period after the parent exited");
  const orphanPid = Number(/orphan (\d+)/.exec(res.stdout)?.[1]);
  if (orphanPid && isAlive(orphanPid)) process.kill(orphanPid);
});
