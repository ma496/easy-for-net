/**
 * Boolean flags for the unattended loop.
 *
 * Pushing is OFF unless the operator asks for it with `AGENT_AUTO_PUSH=1`: work is
 * committed locally and left for a person to push. Refusing to start a drain on a dirty
 * tree is ON (so a concurrent human edit is not parked away); opt out with `0` / `false`.
 *
 * Pure: no filesystem, no process.env reads — callers pass the string they already read.
 */

const OFF = new Set(["0", "false", "off", "no", "none"]);
const ON = new Set(["1", "true", "yes", "on"]);

/**
 * @param {string | undefined} raw  value from the environment (may be undefined)
 * @param {boolean} defaultOn       what to use when unset / blank / unrecognised
 */
export function parseAgentFlag(raw, defaultOn) {
  if (raw == null) return defaultOn;
  const v = String(raw).trim().toLowerCase();
  if (!v) return defaultOn;
  if (OFF.has(v)) return false;
  if (ON.has(v)) return true;
  return defaultOn;
}

/** After each verified commit, push the work branch. Default off. */
export function wantsAutoPush(env = process.env) {
  return parseAgentFlag(env.AGENT_AUTO_PUSH, false);
}

/** Drain exits on dirty tree instead of parking WIP. Default on. */
export function wantsRefuseDirtyStart(env = process.env) {
  return parseAgentFlag(env.AGENT_REFUSE_DIRTY_START, true);
}
