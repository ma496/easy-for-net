/**
 * Which changed paths a runner commit may stage.
 *
 * The runner commits whatever the task changed, so this used to be "everything dirty except
 * build output". That swept the queue's own bookkeeping into whichever commit came next: the
 * previous task's lane move, its build record, the planner's new briefs — each landing one
 * task late, under another task's name, and the last task's never landing at all.
 *
 * So `.agent-queue/` is now staged by name, never wholesale. A task's commit carries exactly
 * one queue change: the removal of its own brief from `todo/`, which is how the shared,
 * tracked queue learns the task is no longer waiting. Everything else there is either local
 * to this machine (`doing/`, `done/`, `failed/`, gitignored) or committed by the step that
 * made it (`queue plan` commits the briefs and `planned.json` it writes). A brief another
 * task left removed — one that failed and went to `failed/` — stays out of this commit.
 *
 * Pure over porcelain entries (`{ index, worktree, path }`), so the rule is testable.
 */

/** Build output and dependencies: never committed, whatever the status says. */
export const BUILD_OUTPUT = /(^|\/)(dist|\.output|\.next|node_modules|bin|obj|coverage)(\/|$)/;

const QUEUE = /^\.agent-queue\//;

/** The tracked path of the brief a task was claimed from. */
export const ownBriefPath = (taskStem) => `.agent-queue/todo/${String(taskStem).replace(/\.md$/, "")}.md`;

/** The entries a commit for `taskStem` (or for no task, when empty) may stage. */
export function stageable(entries, taskStem = "") {
  const own = taskStem ? ownBriefPath(taskStem) : null;
  return (entries ?? []).filter((e) => {
    if (BUILD_OUTPUT.test(e.path)) return false;
    if (!QUEUE.test(e.path)) return true;
    return e.path === own;
  });
}
