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
  const { BASE_BRANCH, PROJECT_NAME, PROTECTED_BRANCHES, REPO_ROOT, WORK_BRANCH, WORKFLOW, workflowRefusal } = await import("../../scripts/lib/project-config.mjs");
  const git = (...args) =>
    // trimEnd, not trim: a porcelain line's leading space is its status column.
    (spawnSync("git", args, { cwd: REPO_ROOT, encoding: "utf8", windowsHide: true }).stdout ?? "").trimEnd();

  const branch = git("rev-parse", "--abbrev-ref", "HEAD") || "unknown";
  const changed = git("status", "--porcelain").split("\n").filter(Boolean);
  // From the branch's remote, else the base, else — with no remote at all — the whole history.
  const { unpushedHere } = await import("../../scripts/lib/remote.mjs");
  const { since, count: ahead, localOnly } = unpushedHere(branch, BASE_BRANCH, REPO_ROOT);
  const { listTasks } = await import("../../scripts/lib/task-names.mjs");
  const lane = (name) => listTasks(join(REPO_ROOT, ".agent-queue", name)).length;

  const out = [`## ${PROJECT_NAME} — session state`, ""];
  out.push(`- Workflow: ${WORKFLOW} (\`project.workflow\`)`);
  out.push(`- Branch: \`${branch}\`${ahead ? (since ? ` (${ahead} commit(s) ahead of ${since})` : localOnly ? ` (${ahead} commit(s), none pushed yet)` : "") : ""}`);
  out.push(`- Uncommitted files: ${changed.length}`);
  if (existsSync(join(REPO_ROOT, ".agent-queue"))) {
    out.push(`- Queue: ${lane("todo")} waiting · ${lane("doing")} in flight · ${lane("failed")} failed`);
  }
  // What past runs learned reaches unattended briefs on its own; an interactive session only
  // sees it if it is pointed at it.
  const lessons = join(REPO_ROOT, ".claude", "memory", "lessons");
  const lessonCount = existsSync(lessons) ? readdirSync(lessons).filter((f) => f.endsWith(".md")).length : 0;
  if (lessonCount) out.push(`- Lessons recorded by past runs: ${lessonCount} — \`npm run lessons\` before a change in an unfamiliar area`);
  if (changed.length > 0) {
    out.push("", "Changed files:", ...changed.slice(0, 12).map((l) => `  ${l}`));
  }
  const refusal = workflowRefusal({ branch });
  if (refusal) {
    out.push(
      "",
      `**On \`${branch}\`, the base branch a team shares (\`project.workflow: "team"\`).** ` +
        "The task queue neither plans nor builds here: work on a branch of your own and open a pull request.",
    );
  } else if (branch === WORK_BRANCH) {
    out.push("", `**On \`${branch}\`, where the task queue commits.** Build and commit here — no task branch, no worktree.`);
    // Only where the guard would refuse the push; a feature branch is the owner's to approve in the turn.
    const PROTECTED = new Set(PROTECTED_BRANCHES);
    if (ahead && PROTECTED.has(branch)) {
      out.push(
        `${ahead} commit(s) are waiting to be pushed. The guard refuses an agent's push to a protected branch — ` +
          "the owner pushes it themselves (`! git push`).",
      );
    }
  }
  console.log(out.join("\n"));
} catch {
  // Orientation is a courtesy. A session must start even when it cannot be given.
}
process.exit(0);
