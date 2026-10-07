/**
 * Resolving a task's `Depends-on:` names against the queue's lanes.
 *
 * A dependency used to be satisfied by any done/ file whose name *contained* it, so
 * `Depends-on: api` was met by every landed task with "api" in its name, and `01-foo` by
 * `01-foobar`. The rule is now exact: a name matches a task file's stem, or that stem with
 * the queue's `YYYY-MM-DDTHH-MM-SS-` intake stamp removed — which is how a planner-written
 * brief refers to a sibling it wrote at the same time.
 */

/** The intake stamp `queue -- add` puts in front of a task's slug. */
const INTAKE_STAMP = /^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}-/;

/**
 * The names a task file answers to: its stem (`scope/name`, lib/task-names.mjs), and that stem
 * with the intake stamp removed from its name part. A dependency is matched against these
 * after lib/task-names.mjs `qualify` has given it a scope, so `01-a` in one plan never
 * matches `01-a` in another.
 */
export function namesOf(file) {
  const stem = String(file).replace(/\\/g, "/").replace(/\.md$/i, "");
  const cut = stem.lastIndexOf("/") + 1;
  const bare = stem.slice(0, cut) + stem.slice(cut).replace(INTAKE_STAMP, "");
  return bare === stem ? [stem] : [stem, bare];
}

/** Whether `dep` names `file`. */
export const names = (dep, file) => namesOf(file).includes(String(dep).replace(/\.md$/i, ""));

/**
 * Where a dependency stands: `landed` when a done/ task has that name or a commit reachable
 * from HEAD names it on a `Task:` line (`landed`, the stems lib/landed-tasks.mjs reads —
 * done/ is local to one machine, history is what a teammate's checkout shares), `pending`
 * when one is waiting or building, `failed` when it went to failed/, and `unknown` when
 * nothing holds it — a typo, or a task that was deleted — which would otherwise hold its
 * dependent in todo/ for ever without saying why.
 */
export function dependencyState(dep, lanes, landed = new Set()) {
  if ((lanes.done ?? []).some((f) => names(dep, f))) return "landed";
  if ([...landed].some((stem) => names(dep, stem))) return "landed";
  if ((lanes.failed ?? []).some((f) => names(dep, f))) return "failed";
  if ([...(lanes.todo ?? []), ...(lanes.doing ?? [])].some((f) => names(dep, f))) return "pending";
  return "unknown";
}
