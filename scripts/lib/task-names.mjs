/**
 * How a task is named, and where its file lives.
 *
 * A task belongs to a scope: the spec that produced it, by its slug (`billing-export`), or
 * `adhoc` for a task queued by hand. Its file sits in that scope's folder in every lane
 * (`.agent-queue/todo/billing-export/01-endpoint.md`), and its *stem*, the identity everything
 * else uses, is the path below the lane: `<scope>/<name>`.
 *
 * The scope is what makes a short name safe. Every spec numbers from `01-`, so `01-endpoint`
 * alone names a task in every spec that has one; the stem names exactly one. It is what the
 * commit's `Task:` line carries, so "landed" can never be satisfied by another spec's task of
 * the same name, and a `Depends-on:` name without a scope means the sibling in the same spec.
 * Planning the same spec again numbers past the names its folder already used, so a stem
 * names one task for good. Build records are grouped differently — by planning, in a
 * date-stamped directory (lib/build-record.mjs).
 *
 * A file directly in a lane (no scope folder) is still read, as a task with no scope.
 */
import { existsSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";

/** The scope of a task queued by hand rather than planned from a spec. */
export const ADHOC = "adhoc";

/** Task files in a lane, as `scope/name.md` (or `name.md` with no scope), sorted. */
export function listTasks(laneDir) {
  if (!existsSync(laneDir)) return [];
  const out = [];
  for (const entry of readdirSync(laneDir)) {
    const full = join(laneDir, entry);
    if (entry.endsWith(".md")) out.push(entry);
    else if (statSync(full).isDirectory()) {
      for (const f of readdirSync(full)) if (f.endsWith(".md")) out.push(`${entry}/${f}`);
    }
  }
  return out.sort();
}

/** A task file — `scope/name.md`, or a path into a lane — reduced to its stem, `scope/name`. */
export function stemOf(file) {
  const p = String(file ?? "").replace(/\\/g, "/");
  const inLane = /(?:^|\/)\.agent-queue\/(?:todo|doing|done|failed)\/(.+?)\.md$/i.exec(p);
  if (inLane) return inLane[1];
  if (/^[a-zA-Z]:\/|^\//.test(p)) return p.replace(/^.*\//, "").replace(/\.md$/i, "");
  return p.replace(/\.md$/i, "");
}

/** The scope part of a stem, or "" for none. */
export const scopeOf = (stem) => {
  const i = String(stem).lastIndexOf("/");
  return i < 0 ? "" : String(stem).slice(0, i);
};

/** The name part of a stem. */
export const nameOf = (stem) => String(stem).slice(String(stem).lastIndexOf("/") + 1);

/** A stem as one path segment, for keys that become a single file or directory name. */
export const fsKey = (stem) => String(stem).replace(/\//g, "__");

/**
 * The stem a `Depends-on:` name refers to from the task `fromStem`. A name with a scope
 * stands as written; a bare one is the sibling in the same scope.
 */
export function qualify(dep, fromStem) {
  const d = String(dep).trim().replace(/\.md$/i, "");
  if (d.includes("/")) return d;
  const scope = scopeOf(fromStem);
  return scope ? `${scope}/${d}` : d;
}
