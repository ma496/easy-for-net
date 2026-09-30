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
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { join } from "node:path";

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

/**
 * One hash for the working tree's whole change against HEAD: tracked edits (staged or not)
 * and the content of every untracked file that is not ignored.
 *
 * Two equal fingerprints mean verify would be judging the same bytes, which is what lets the
 * runner reuse a verify that just passed instead of spending another gate on an unchanged
 * tree — the attempt after a missed review, and the salvage check that opens it, both used
 * to re-run the full gate on exactly the tree the last one had passed.
 */
export function treeFingerprint(cwd = process.cwd()) {
  const hash = createHash("sha256");
  const git = (args) => spawnSync("git", args, { cwd, maxBuffer: 256 * 1024 * 1024 });
  hash.update(git(["rev-parse", "HEAD"]).stdout ?? "");
  hash.update(git(["diff", "HEAD", "--binary"]).stdout ?? "");
  const untracked = String(git(["ls-files", "--others", "--exclude-standard", "-z"]).stdout ?? "")
    .split("\0")
    .filter(Boolean)
    .sort();
  for (const path of untracked) {
    hash.update(`\0${path}\0`);
    try {
      hash.update(readFileSync(join(cwd, path)));
    } catch {
      hash.update("<unreadable>");
    }
  }
  return hash.digest("hex");
}
