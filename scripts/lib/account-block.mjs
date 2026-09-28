/**
 * When the account itself is refusing, and when it will stop.
 *
 * An account-level refusal is not any task's fault, so the drain puts the brief back and
 * stops — correct, and for a while that was the whole story because the timer only came
 * round once an hour. On a one-minute timer it is a spin: launchd starts a drain, the
 * account refuses in two seconds, the drain stops, and a minute later it happens again.
 * The journal holds 1,986 such runs, 214 of them against one brief in a single night, each
 * costing nothing and meaning nothing except noise in the log and a task that looks like it
 * failed 217 times.
 *
 * The refusal says when it lifts — "You've hit your session limit · resets 6:10am
 * (Asia/Karachi)" — so the fix is to believe it: record the time, and refuse to start until
 * it has passed. Parsing lives here, apart from the I/O, because a clock is the one thing
 * worth testing and the hardest thing to test through a file.
 */
import { existsSync, readFileSync, writeFileSync, unlinkSync, mkdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
export const BLOCK_FILE = join(ROOT, ".agent-runs", "account-blocked-until");

/**
 * The moment a refusal says the account recovers, or null when it does not say.
 *
 * The hour is read in the machine's own zone rather than the one named in the message. They
 * are the same zone in practice — the message states the account's local time and the
 * machine runs in it — and guessing at an offset for a zone abbreviation we cannot resolve
 * would turn a five-minute wait into a five-hour one. A time that has already passed today
 * means tomorrow, which is what "resets 1:10am" said at 8pm.
 *
 * Returns null rather than throwing on anything unrecognised: a refusal whose wording
 * changes must degrade to the old behaviour (stop, try again next tick), never to a
 * backoff of some accidental length.
 */
export function parseResetAt(text, now = new Date()) {
  const m = /resets?\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?/i.exec(String(text ?? ""));
  if (!m) return null;

  let hour = Number(m[1]);
  const minute = m[2] ? Number(m[2]) : 0;
  const meridiem = m[3]?.toLowerCase();
  if (!Number.isInteger(hour) || hour < 0 || hour > 23) return null;
  if (minute < 0 || minute > 59) return null;
  if (meridiem === "pm" && hour < 12) hour += 12;
  if (meridiem === "am" && hour === 12) hour = 0;
  if (!meridiem && hour > 23) return null;

  const at = new Date(now);
  at.setHours(hour, minute, 0, 0);
  // "resets 1:10am" said at 8pm means tomorrow. Equal counts as past: a reset at exactly
  // now has already happened.
  if (at.getTime() <= now.getTime()) at.setDate(at.getDate() + 1);
  return at;
}

/** Remember that the account is refusing until `at`. No-op when the refusal named no time. */
export function recordAccountBlock(text, now = new Date()) {
  const at = parseResetAt(text, now);
  if (!at) return null;
  mkdirSync(dirname(BLOCK_FILE), { recursive: true });
  writeFileSync(BLOCK_FILE, `${at.toISOString()}\n${String(text ?? "").trim()}\n`);
  return at;
}

/**
 * How long the account is still refusing for, or null when it is not.
 *
 * Clears the marker once the time has passed, so the caller never has to and a stale file
 * cannot hold the loop down. An unreadable or malformed marker is treated as no block —
 * the failure mode of this file must be "the loop runs", not "the loop never runs again".
 */
export function accountBlockedUntil(now = new Date()) {
  if (!existsSync(BLOCK_FILE)) return null;
  let at;
  try {
    at = new Date(readFileSync(BLOCK_FILE, "utf8").split("\n")[0].trim());
  } catch {
    return null;
  }
  if (Number.isNaN(at.getTime())) {
    try { unlinkSync(BLOCK_FILE); } catch { /* nothing to do */ }
    return null;
  }
  if (at.getTime() <= now.getTime()) {
    try { unlinkSync(BLOCK_FILE); } catch { /* the next call will try again */ }
    return null;
  }
  return at;
}

/** Minutes until `at`, rounded up, for a message a person reads. */
export function minutesUntil(at, now = new Date()) {
  return Math.max(1, Math.ceil((at.getTime() - now.getTime()) / 60000));
}
