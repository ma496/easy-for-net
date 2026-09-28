#!/usr/bin/env node
/**
 * SessionStart hook: put the state of the working tree and the queue into context, so the
 * first answer of a session is grounded in what is actually there rather than in what the
 * last session remembers.
 *
 * stdout is added to the session context. Keep it fast (~1s) and never fail the session.
 */
import { spawnSync } from "node:child_process";
import { existsSync, readdirSync } from "node:fs";
import { join } from "node:path";

try {
  const { PROJECT_NAME, REPO_ROOT, WORK_BRANCH } = await import("../../scripts/lib/project-config.mjs");
  const git = (...args) =>
    (spawnSync("git", args, { cwd: REPO_ROOT, encoding: "utf8", windowsHide: true }).stdout ?? "").trim();

  const branch = git("rev-parse", "--abbrev-ref", "HEAD") || "unknown";
  const changed = git("status", "--porcelain").split("\n").filter(Boolean);
  const ahead = Number(git("rev-list", "--count", `origin/${branch}..${branch}`) || 0);
  const lane = (name) => {
    const dir = join(REPO_ROOT, ".agent-queue", name);
    return existsSync(dir) ? readdirSync(dir).filter((f) => f.endsWith(".md")).length : 0;
  };

  const out = [`## ${PROJECT_NAME} — session state`, ""];
  out.push(`- Branch: \`${branch}\`${ahead ? ` (${ahead} commit(s) ahead of origin)` : ""}`);
  out.push(`- Uncommitted files: ${changed.length}`);
  if (existsSync(join(REPO_ROOT, ".agent-queue"))) {
    out.push(`- Queue: ${lane("todo")} waiting · ${lane("doing")} in flight · ${lane("failed")} failed`);
  }
  if (changed.length > 0) {
    out.push("", "Changed files:", ...changed.slice(0, 12).map((l) => `  ${l}`));
  }
  if (branch === WORK_BRANCH) {
    out.push("", `**On \`${branch}\`, where the task queue commits.** Build and commit here — no task branch, no worktree.`);
    if (ahead) out.push(`${ahead} commit(s) are waiting to be pushed. Pushing needs the owner's say-so in that turn.`);
  }
  console.log(out.join("\n"));
} catch {
  // Orientation is a courtesy. A session must start even when it cannot be given.
}
process.exit(0);
