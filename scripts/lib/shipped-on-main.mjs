/**
 * Is a `done/` brief's claimed ship actually on `main`?
 *
 * The audit used to reconcile *records against commits*: a brief with a build record and a
 * matching SHA looked fine even when that record lived only in the working tree and the
 * Commit cell said `*(uncommitted)*`. Seven features once sat that way — filed in `done/`,
 * records on disk, nothing on `main` — and the audit put them in no class at all, because
 * the path that found no SHA simply `continue`d.
 *
 * This helper answers the direct question. Pure over plain facts so the classification can
 * be unit-tested without a repository in a particular state.
 */

/**
 * Parse the Commit cell of a build record. Returns a 40-or-short hex SHA, or null when the
 * record admits there is no commit (uncommitted / none / empty / missing).
 */
export function commitShaFromRecord(text) {
  const cell = /\*\*Commit\*\*\s*\|\s*([^\n|]+)/i.exec(text ?? "");
  if (!cell) return null;
  const raw = cell[1].trim();
  if (!raw || /\buncommitted\b/i.test(raw) || /\bnone\b/i.test(raw)) return null;
  const sha = /^`?([0-9a-f]{7,40})`?$/i.exec(raw);
  return sha ? sha[1].toLowerCase() : null;
}

/**
 * Classify one done brief against what main actually holds.
 *
 * @param {{
 *   hasRecord: boolean,
 *   recordOnMain: boolean,
 *   commitSha: string | null,
 *   commitOnMain: boolean,
 * }} input
 * @returns {"clean" | "unrecorded" | "not_on_main"}
 */
export function classifyDoneShip({ hasRecord, recordOnMain, commitSha, commitOnMain }) {
  if (!hasRecord) return "unrecorded";
  // The record itself must be on main — an untracked docs/builds/*.md is the clearest lie.
  if (!recordOnMain) return "not_on_main";
  if (!commitSha) return "not_on_main";
  if (!commitOnMain) return "not_on_main";
  return "clean";
}
