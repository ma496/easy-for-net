#!/usr/bin/env node
/**
 * Status line: branch, working-tree state, and whether a drain is running.
 *
 * Node rather than a shell script so it reads the same on Windows: the drain lock holds a
 * Windows pid there, which a Git Bash `kill -0` cannot see.
 */
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const git = (...args) => (spawnSync("git", args, { cwd: root, encoding: "utf8", windowsHide: true }).stdout ?? "").trim();

let name = "";
let configured = "";
let base = "";
let team = false;
try {
  const cfg = JSON.parse(readFileSync(join(root, "agentic.config.json"), "utf8"));
  name = cfg.project?.name ?? "";
  configured = cfg.project?.branch ?? "";
  team = cfg.project?.workflow === "team";
  base = cfg.project?.baseBranch ?? "";
} catch {
  /* no config — the line still shows the branch */
}

let branch = git("rev-parse", "--abbrev-ref", "HEAD") || "-";
const dirty = git("status", "--porcelain").split("\n").filter(Boolean).length;
// A branch the config names as protected is marked, because a commit there is normal and a
// push there is not. In a team the shared base branch is marked too: the queue refuses to
// build there, and the mark says why before the refusal does. Kept to what is cheap to read
// — origin/HEAD, else main or master — since this runs on every prompt.
if (team && !base) {
  base = git("symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD").replace(/^origin\//, "");
  if (!base) base = ["main", "master"].includes(branch) ? branch : "";
}
if (team && branch === base) branch = `⛔ ${branch} (team: use a branch)`;
else if (configured && branch === configured) branch = `⚠ ${branch}`;

let drain = "drain○";
const lock = join(root, ".agent-queue", "drain.lock");
if (existsSync(lock)) {
  const pid = Number(readFileSync(lock, "utf8").trim());
  let alive = false;
  try {
    process.kill(pid, 0);
    alive = true;
  } catch (err) {
    alive = err.code === "EPERM";
  }
  drain = alive ? "drain●" : "drain?";
}

console.log(`${branch}${dirty ? `*${dirty}` : ""} │ ${drain}${name ? ` │ ${name}` : ""}`);
