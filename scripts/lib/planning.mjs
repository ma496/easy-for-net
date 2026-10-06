/**
 * The rules intake applies around the planner: which specs it may plan, and whether the
 * task files it wrote are acceptable. Pure, so they are testable without a model or a repo.
 */
import { ADHOC } from "./task-names.mjs";

/**
 * A spec file's name as its scope — the folder its tasks live in: `Billing Export.md` →
 * `billing-export`. A spec called `adhoc` is kept out of the hand-queued tasks' scope.
 */
export function specSlug(specFile) {
  const slug =
    String(specFile)
      .replace(/\.md$/i, "")
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "") || "spec";
  return slug === ADHOC ? `${ADHOC}-spec` : slug;
}

/**
 * Everything wrong with the task files one planning call added, one message per file.
 *
 * Every spec numbers its tasks from `01-`, so a name is only unique inside its spec's scope
 * (lib/task-names.mjs): the call is told to write into `.agent-queue/todo/<scope>/`, and a
 * file anywhere else — loose in todo/, or in another spec's folder — would be read as part
 * of the wrong plan, so it is refused rather than trusted. Planning an edited spec again
 * writes into the same folder, so a name it already used there — still queued, or landed in
 * history — is refused too: the new task would be read as the old one, already landed.
 *
 * `added` the files the call wrote, as `scope/name.md`; `scope` the one it was given;
 * `taken` the names (without `.md`) already used in it.
 */
export function planNameProblems({ added, scope, taken = [] }) {
  const used = new Set(taken);
  const problems = [];
  for (const file of added) {
    const slash = file.lastIndexOf("/");
    const dir = slash < 0 ? "" : file.slice(0, slash);
    const name = file.slice(slash + 1);
    if (dir !== scope) {
      problems.push(`${file}: must be written in .agent-queue/todo/${scope}/`);
    } else if (!/^\d{2}-[a-z0-9][a-z0-9-]*\.md$/.test(name)) {
      problems.push(`${file}: must be named NN-short-slug.md`);
    } else if (used.has(name.replace(/\.md$/, ""))) {
      problems.push(`${file}: ${scope} already has a task by this name`);
    }
  }
  return problems;
}

/** The number a new plan of a spec starts from: one past the highest `NN-` already used. */
export function nextNumber(taken = []) {
  const max = taken.reduce((m, n) => Math.max(m, Number(/^(\d{2})-/.exec(n)?.[1] ?? 0)), 0);
  return String(max + 1).padStart(2, "0");
}

/**
 * The spec files (top level of `specs/`, by file name) among `changedPaths`.
 *
 * In a team, a spec is planned on the branch it was written on, by the developer who wrote
 * it. A spec that reached this branch from the base branch was planned there already — and
 * one that somehow was not belongs to whoever wrote it — so intake considers only the specs
 * this branch itself added or changed: what differs from the merge base, plus what is not
 * committed yet.
 */
export function specsChangedOn(changedPaths) {
  const out = new Set();
  for (const raw of changedPaths ?? []) {
    const p = String(raw).trim().replace(/\\/g, "/").replace(/^"|"$/g, "");
    const m = /^specs\/([^/]+\.md)$/i.exec(p);
    if (m) out.add(m[1]);
  }
  return out;
}
