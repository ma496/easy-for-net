/**
 * Which gate steps a change needs, decided from the paths it touched.
 *
 * A full gate runs every suite whatever changed: a change to one markdown file still builds
 * the solution, runs every backend test, lints and typechecks the web app and builds it for
 * production — minutes of work that cannot fail because of that change. An unattended task
 * verifies several times per attempt, so those minutes are paid over and over.
 *
 * Each step names the paths that can break it (`watches`). A step runs when a changed path
 * matches one of them. A path no step watches is either declared inert (documentation, queue
 * bookkeeping) or unknown — and an unknown path runs the whole gate, because a scope that
 * guesses wrong about what a file affects is a gate that passes without testing the change.
 *
 * Pure: steps and paths in, a decision out. The stack-specific rules live with the gate.
 */

/**
 * @param {Array<{ name: string, watches?: RegExp[] }>} steps
 *   every step of the gate. A step with no `watches` is treated as watching everything.
 * @param {string[]} paths repository-relative, forward slashes
 * @param {{ inert?: RegExp[] }} [options] paths no step needs to see
 * @returns {{ full: boolean, reason: string, steps: typeof steps, unknown: string[] }}
 *   `full` when every step runs; `unknown` lists the paths that forced that.
 */
export function selectGateSteps(steps, paths, { inert = [] } = {}) {
  const changed = (paths ?? []).map((p) => String(p).replace(/\\/g, "/")).filter(Boolean);
  if (changed.length === 0) {
    return { full: true, reason: "no changed paths were given, so every step runs", steps, unknown: [] };
  }

  const watches = (step) => step.watches ?? null;
  const unknown = changed.filter(
    (p) => !inert.some((re) => re.test(p)) && !steps.some((s) => watches(s) === null || watches(s).some((re) => re.test(p))),
  );
  if (unknown.length > 0) {
    return {
      full: true,
      reason: `${unknown.length} changed path(s) are watched by no step, so every step runs`,
      steps,
      unknown,
    };
  }

  const selected = steps.filter((s) => watches(s) === null || changed.some((p) => watches(s).some((re) => re.test(p))));
  return {
    full: selected.length === steps.length,
    reason: `${selected.length} of ${steps.length} step(s) watch the ${changed.length} changed path(s)`,
    steps: selected,
    unknown: [],
  };
}
