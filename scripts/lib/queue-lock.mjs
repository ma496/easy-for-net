/**
 * The queue's single-writer lock.
 *
 * A task is claimed by renaming it out of todo/, which is not atomic across processes: two
 * drains list the same todo/ a moment apart, both try the rename, and the loser dies part-way
 * through. So every mutating path (intake, claim, lane moves, audit repairs) runs under one
 * lock — a file created with the `wx` flag, so creating it is itself the test.
 *
 * Three ways the first version of this went wrong, each handled here:
 *
 *   - **Stale takeover raced.** Two processes that both saw a dead holder both unlinked the
 *     file, and the slower one deleted the faster one's fresh lock; both then ran. Taking
 *     over now happens under a second, short-lived `wx` file, and the stale lock is removed
 *     only if it still holds exactly what was judged stale.
 *   - **Windows reuses pids.** A dead drain's pid can belong to an unrelated live process
 *     minutes later, and the lock then looked held for ever. The holder now refreshes the
 *     file's `heartbeatAt` while it runs; a lock whose heartbeat is older than `staleMs` is
 *     stale whatever its pid says.
 *   - **A lock on a shared checkout** names a pid on another machine, which says nothing
 *     here. Only the heartbeat is trusted for a lock written by another host.
 *
 * Every function takes its clock, host and liveness check as arguments, so the rules are
 * testable without spawning processes.
 */
import { readFileSync, statSync, unlinkSync, writeFileSync } from "node:fs";
import { hostname } from "node:os";

/** How long a holder may go without refreshing its heartbeat before its lock is stale. */
export const DEFAULT_STALE_MS = 30 * 60_000;
/** How often a holder refreshes it. Well inside the stale window. */
export const HEARTBEAT_MS = 60_000;
/** A takeover file older than this was left by a process that died mid-takeover. */
const TAKEOVER_STALE_MS = 30_000;

/** The lock's contents, or null. A lock written by an older version holds a bare pid. */
export function parseLock(text) {
  const raw = String(text ?? "").trim();
  if (!raw) return null;
  if (/^\d+$/.test(raw)) return { pid: Number(raw), host: null, startedAt: null, heartbeatAt: null, legacy: true };
  try {
    const v = JSON.parse(raw);
    return v && Number.isInteger(v.pid) ? v : null;
  } catch {
    return null;
  }
}

/**
 * Whether a lock is still held. Pure: `now`, `host` and `isAlive` are supplied.
 *
 * A legacy lock has no heartbeat, so its file's modification time stands in for one.
 */
export function isHeld(lock, { now = Date.now(), host = hostname(), isAlive, staleMs = DEFAULT_STALE_MS, mtimeMs = null } = {}) {
  if (!lock) return false;
  const beat = Date.parse(lock.heartbeatAt ?? "") || mtimeMs || 0;
  const fresh = now - beat < staleMs;
  if (lock.host && lock.host !== host) return fresh;
  return Boolean(isAlive?.(lock.pid)) && fresh;
}

const readRaw = (path) => {
  try {
    return readFileSync(path, "utf8");
  } catch {
    return null;
  }
};
const mtimeOf = (path) => {
  try {
    return statSync(path).mtimeMs;
  } catch {
    return null;
  }
};

/**
 * Try once to take the lock. Returns `{ ok: true, lock }` or `{ ok: false, holder }`.
 *
 * `ok: false` with `holder: null` means the lock changed hands under us mid-takeover; the
 * caller may simply try again.
 */
export function tryAcquire(path, { pid = process.pid, now = Date.now(), host = hostname(), isAlive, staleMs = DEFAULT_STALE_MS } = {}) {
  const mine = { pid, host, startedAt: new Date(now).toISOString(), heartbeatAt: new Date(now).toISOString() };
  const create = () => {
    try {
      writeFileSync(path, `${JSON.stringify(mine)}\n`, { flag: "wx" });
      return true;
    } catch (err) {
      if (err.code === "EEXIST") return false;
      throw err;
    }
  };
  if (create()) return { ok: true, lock: mine };

  const seen = readRaw(path);
  const holder = parseLock(seen);
  if (holder && isHeld(holder, { now, host, isAlive, staleMs, mtimeMs: mtimeOf(path) })) {
    return { ok: false, holder };
  }

  // Stale. Take it over under a second lock, so only one process judges and replaces it.
  const takeover = `${path}.takeover`;
  const takeoverAge = mtimeOf(takeover);
  if (takeoverAge !== null && now - takeoverAge > TAKEOVER_STALE_MS) {
    try {
      unlinkSync(takeover);
    } catch {
      // Someone else cleared it.
    }
  }
  try {
    writeFileSync(takeover, String(pid), { flag: "wx" });
  } catch (err) {
    if (err.code === "EEXIST") return { ok: false, holder: null };
    throw err;
  }
  try {
    // Remove it only if it is still exactly the lock that was judged stale. If another
    // process replaced it in between, that lock is live and is left alone.
    if (readRaw(path) === seen) {
      try {
        unlinkSync(path);
      } catch {
        // Already gone.
      }
    }
    return create() ? { ok: true, lock: mine, tookOver: holder } : { ok: false, holder: null };
  } finally {
    try {
      unlinkSync(takeover);
    } catch {
      // Nothing to clean.
    }
  }
}

/**
 * Who holds the lock at `path` right now, or null when nobody does (no file, or a stale
 * one). For a process that must not run beside a drain but does not take the lock itself.
 */
export function liveHolder(path, { now = Date.now(), host = hostname(), isAlive, staleMs = DEFAULT_STALE_MS } = {}) {
  const holder = parseLock(readRaw(path));
  return holder && isHeld(holder, { now, host, isAlive, staleMs, mtimeMs: mtimeOf(path) }) ? holder : null;
}

/** Refresh the heartbeat — only while the file is still ours. */
export function heartbeat(path, lock, now = Date.now()) {
  const current = parseLock(readRaw(path));
  if (!current || current.pid !== lock.pid || current.startedAt !== lock.startedAt) return false;
  lock.heartbeatAt = new Date(now).toISOString();
  writeFileSync(path, `${JSON.stringify(lock)}\n`);
  return true;
}

/** Remove the lock — only while it is still ours. */
export function release(path, lock) {
  const current = parseLock(readRaw(path));
  if (!current || current.pid !== lock.pid || current.startedAt !== lock.startedAt) return false;
  try {
    unlinkSync(path);
    return true;
  } catch {
    return false;
  }
}
