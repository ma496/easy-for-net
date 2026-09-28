/**
 * Whether a commit actually built something, or only moved paperwork.
 *
 * A task finishes by moving its brief out of `todo/` and writing a build record, and both of
 * those are commits. So `HEAD` moving proves nothing: six tasks once landed exactly that
 * way — six commit subjects claiming real features, one of them carrying any application
 * code — and the queue, the build records and the audit all called them done. The dashboard
 * they described had not changed by a single line.
 *
 * The test is deliberately crude, because a subtle one would be argued with: a commit counts
 * as work when it touches a file outside the queue's own bookkeeping. That does misjudge the
 * rare genuine bookkeeping commit — deleting a duplicated brief, say — but reporting one of
 * those for a human to wave through costs a sentence, and accepting a whole feature that was
 * never written costs a day.
 *
 * Pure functions over path strings. Which paths are bookkeeping is `unownedPaths` in
 * agentic.config.json, so a project that moves its build records moves this rule with them.
 */

import { compileRules, config } from "./project-config.mjs";

/** Paths that are the queue talking about itself rather than the product. */
const BOOKKEEPING = compileRules(config.unownedPaths, "unowned path");

/** The files in a commit that are the work rather than the paperwork. */
export function productFiles(files) {
  return (files ?? [])
    .map((f) => String(f).trim())
    .filter(Boolean)
    .filter((f) => !BOOKKEEPING.some((re) => re.test(f)));
}

/** True when a commit changed something outside the queue's bookkeeping. */
export function carriesProductCode(files) {
  return productFiles(files).length > 0;
}

/** `git show --name-only --format=` output → the same verdict. */
export function carriesProductCodeFromShow(stdout) {
  return carriesProductCode(String(stdout ?? "").split("\n"));
}
