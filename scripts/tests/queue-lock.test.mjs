import { strict as assert } from "node:assert";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { heartbeat, isHeld, parseLock, release, tryAcquire } from "../lib/queue-lock.mjs";

const HOUR = 60 * 60_000;
const withDir = (fn) => {
  const dir = mkdtempSync(join(tmpdir(), "queue-lock-"));
  try {
    return fn(join(dir, "drain.lock"));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
};
const alive = (pids) => (pid) => pids.includes(pid);

test("a free lock is taken and records who holds it", () =>
  withDir((path) => {
    const res = tryAcquire(path, { pid: 10, host: "h", isAlive: alive([10]) });
    assert.equal(res.ok, true);
    const lock = parseLock(readFileSync(path, "utf8"));
    assert.equal(lock.pid, 10);
    assert.equal(lock.host, "h");
  }));

test("a live holder with a fresh heartbeat keeps the lock", () =>
  withDir((path) => {
    tryAcquire(path, { pid: 10, host: "h", isAlive: alive([10]) });
    const res = tryAcquire(path, { pid: 11, host: "h", isAlive: alive([10, 11]) });
    assert.equal(res.ok, false);
    assert.equal(res.holder.pid, 10);
  }));

test("a dead holder's lock is taken over", () =>
  withDir((path) => {
    tryAcquire(path, { pid: 10, host: "h", isAlive: alive([10]) });
    const res = tryAcquire(path, { pid: 11, host: "h", isAlive: alive([11]) });
    assert.equal(res.ok, true);
    assert.equal(res.tookOver.pid, 10);
    assert.equal(parseLock(readFileSync(path, "utf8")).pid, 11);
  }));

test("a reused pid does not hold a lock whose heartbeat stopped", () => {
  const lock = { pid: 10, host: "h", heartbeatAt: new Date(0).toISOString() };
  assert.equal(isHeld(lock, { now: 2 * HOUR, host: "h", isAlive: () => true }), false);
  assert.equal(isHeld({ ...lock, heartbeatAt: new Date(2 * HOUR - 1000).toISOString() }, { now: 2 * HOUR, host: "h", isAlive: () => true }), true);
});

test("another host's lock is judged by its heartbeat alone", () => {
  const lock = { pid: 10, host: "other", heartbeatAt: new Date(HOUR).toISOString() };
  assert.equal(isHeld(lock, { now: HOUR + 1000, host: "h", isAlive: () => false }), true, "a pid there says nothing here");
  assert.equal(isHeld(lock, { now: 3 * HOUR, host: "h", isAlive: () => true }), false);
});

test("a legacy bare-pid lock is still understood", () => {
  assert.deepEqual(parseLock("1234\n").pid, 1234);
  assert.equal(isHeld(parseLock("1234"), { now: 1000, host: "h", isAlive: () => true, mtimeMs: 500 }), true);
  assert.equal(isHeld(parseLock("1234"), { now: 1000, host: "h", isAlive: () => false, mtimeMs: 500 }), false);
});

test("a stale lock replaced by someone else mid-takeover is left alone", () =>
  withDir((path) => {
    // A takeover is already in progress elsewhere: this process must not also take over.
    writeFileSync(path, JSON.stringify({ pid: 10, host: "h", heartbeatAt: new Date().toISOString() }));
    writeFileSync(`${path}.takeover`, "99");
    const res = tryAcquire(path, { pid: 11, host: "h", isAlive: alive([11]) });
    assert.equal(res.ok, false);
    assert.equal(res.holder, null, "retryable, not a live holder");
    assert.equal(parseLock(readFileSync(path, "utf8")).pid, 10);
  }));

test("heartbeat and release touch only a lock that is still ours", () =>
  withDir((path) => {
    const { lock } = tryAcquire(path, { pid: 10, host: "h", isAlive: alive([10]) });
    assert.equal(heartbeat(path, lock, Date.now() + 1000), true);
    const stranger = { pid: 99, startedAt: "x" };
    assert.equal(heartbeat(path, stranger), false);
    assert.equal(release(path, stranger), false);
    assert.equal(release(path, lock), true);
    assert.equal(release(path, lock), false);
  }));
