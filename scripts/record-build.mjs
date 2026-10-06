#!/usr/bin/env node
/**
 * The build record: one committed markdown file per task the queue lands.
 *
 *   npm run record -- --task <brief.md>
 *
 * The runner calls this once a task has verified and *before* it commits, so the record is
 * part of the task's own commit. It used to run after the commit, which left it — and the
 * brief's lane move — uncommitted until the next task's commit swept them up under the
 * wrong name, with the last task of every drain left dirty.
 *
 * A record preserves the brief verbatim, with the files the task changed. It lives in the
 * directory its spec's planning named (`docs/builds/<date-time>-<spec>/`), or in
 * `docs/builds/adhoc/` for a task queued by hand. It cannot name its own commit's hash —
 * that commit does not exist yet — so the commit names the task on its `Task:` line.
 *
 * This never fails a run: a task that lands unrecorded is a documentation gap, a task that
 * fails because a markdown file could not be written is a broken pipeline. Every error is
 * reported and the exit code stays 0, unless it was invoked with bad arguments.
 */
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { recordPathFor, renderRecord } from "./lib/build-record.mjs";
import { stemOf } from "./lib/task-names.mjs";
import { config } from "./lib/project-config.mjs";
import { stageable } from "./lib/stage-paths.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const BUILDS = String(config.docs.builds).replace(/[\\/]+$/, "");

const argv = process.argv.slice(2);
const i = argv.indexOf("--task");
const taskFile = i >= 0 ? argv[i + 1] : null;
if (!taskFile) {
  console.error("Usage: npm run record -- --task <brief.md>");
  process.exit(2);
}

const git = (args) => spawnSync("git", args, { cwd: ROOT, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });

/**
 * What the task's commit will hold, as `{ file, add, del }`: the same paths auto-ship will
 * stage (lib/stage-paths.mjs), measured against HEAD. An untracked file has no diff yet, so
 * its lines are counted as added.
 */
function changedFiles(stem) {
  const entries = (git(["status", "--porcelain", "--untracked-files=all"]).stdout ?? "")
    .replace(/\n+$/, "")
    .split("\n")
    .filter(Boolean)
    .map((line) => ({
      index: line[0],
      worktree: line[1],
      path: line.slice(3).trim().replace(/^.* -> /, "").replace(/^"|"$/g, ""),
    }));
  return stageable(entries, stem)
    .filter((e) => !e.path.startsWith(".agent-queue/") && !e.path.startsWith(`${BUILDS}/`))
    .map((e) => {
      if (e.index === "?") {
        let add = "-";
        try {
          add = String(readFileSync(join(ROOT, e.path), "utf8").split("\n").length);
        } catch {
          /* a binary or unreadable file: shown without a count */
        }
        return { file: e.path, add, del: "0" };
      }
      const [add = "-", del = "-"] = (git(["diff", "HEAD", "--numstat", "--", e.path]).stdout ?? "").split("\t");
      return { file: e.path, add, del };
    });
}

try {
  const stem = stemOf(taskFile);
  const brief = readFileSync(taskFile, "utf8");
  const now = new Date();
  let planned = {};
  try {
    planned = JSON.parse(readFileSync(join(ROOT, ".agent-queue", "planned.json"), "utf8"));
  } catch {
    /* nothing planned yet */
  }
  const existing = existsSync(join(ROOT, BUILDS)) ? readdirSync(join(ROOT, BUILDS)) : [];
  const rel = recordPathFor(stem, { planned, existing, date: now });
  const target = join(ROOT, BUILDS, rel);
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, renderRecord({ stem, brief, date: now, files: changedFiles(stem), verify: config.commands.verify }));
  console.log(`Recorded ${BUILDS}/${rel}`);
} catch (err) {
  console.error(`record-build: ${err.message}`);
}
