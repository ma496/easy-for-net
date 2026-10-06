/**
 * Which tasks have landed, read from git rather than from the queue's lanes.
 *
 * `done/` is one machine's record: it is gitignored, so a fresh clone, a teammate, or the
 * same developer on another computer starts with it empty. What every checkout of a branch
 * does share is its history, and every commit the runner makes names its brief on a
 * `Task: <stem>` line. A task is landed on this branch exactly when such a line is reachable
 * from HEAD — which stays true through a rebase (the message travels with the commit) and
 * through a squash merge (the squashed message keeps each commit's body).
 */
import { spawnSync } from "node:child_process";
import { taskTrailersIn } from "./commit-pairing.mjs";
import { REPO_ROOT } from "./project-config.mjs";

/** Every task stem named by a commit message in `log`, as a Set. Pure. */
export function landedStemsIn(log) {
  return new Set(taskTrailersIn(log));
}

/**
 * The stems of every task whose commit is reachable from HEAD. An empty set when git cannot
 * answer — a repository with no commits yet — so a dependency is then simply not landed.
 */
export function landedTaskStems(root = REPO_ROOT) {
  const res = spawnSync("git", ["log", "--format=%B"], {
    cwd: root,
    encoding: "utf8",
    maxBuffer: 256 * 1024 * 1024,
    windowsHide: true,
  });
  return res.status === 0 ? landedStemsIn(res.stdout) : new Set();
}
