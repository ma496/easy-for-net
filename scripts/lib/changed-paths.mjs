/**
 * Which files the working tree has changed, one path per file.
 *
 * Every decision the runner makes from a diff — which departments owe a review, which skills
 * the change's shape calls for, which live checks verify must run — matches rules against
 * these paths. Plain `git status --porcelain` reports a brand-new directory as a single
 * `?? src/.../reports/` line, and a rule like `app/.*\.tsx$` never matches a directory: a
 * task that added a whole new page therefore owed no design review at all. So untracked
 * files are listed individually, and `-z` is used so a path with spaces or non-ASCII
 * characters arrives as written rather than quoted and escaped.
 */
import { spawnSync } from "node:child_process";

/** The arguments that produce what `parsePorcelainZ` reads. */
export const PORCELAIN_ARGS = ["status", "--porcelain", "-z", "--untracked-files=all"];

/**
 * Parse `git status --porcelain -z` output into paths.
 *
 * Each entry is `XY <path>\0`; a rename or copy (`R`/`C` in either column) is followed by a
 * second NUL-terminated field holding the path it came from. The new path is the one that
 * changed; the old one is reported too when `includeRenamedFrom` is set, since deleting it
 * is also part of the change.
 */
export function parsePorcelainZ(output, { includeRenamedFrom = false } = {}) {
  const fields = String(output ?? "").split("\0");
  const paths = [];
  for (let i = 0; i < fields.length; i++) {
    const entry = fields[i];
    if (entry.length < 4) continue;
    const status = entry.slice(0, 2);
    paths.push(entry.slice(3));
    if (/[RC]/.test(status)) {
      const from = fields[i + 1];
      i += 1;
      if (includeRenamedFrom && from) paths.push(from);
    }
  }
  return paths;
}

/** The working tree's changed paths, untracked files listed one by one. */
export function workingTreePaths(cwd = process.cwd(), options = {}) {
  const res = spawnSync("git", PORCELAIN_ARGS, { cwd, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  return parsePorcelainZ(res.stdout ?? "", options);
}
