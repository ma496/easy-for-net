/**
 * Salvage prior attempt work so a retry does not rebuild from zero.
 *
 * When a task fails (turn limit, session limit after writing, verify fail, etc.) the drain
 * parks the dirty tree under `.agent-runs/interrupted/<slug>/`. The next drain restores
 * that park before `agent-run` starts; the runner then verifies first and tells Claude to
 * review-only or fix-only — never "start the feature again".
 *
 * Pure-ish helpers: callers own logging. `git apply` and renames are the only I/O.
 */
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  renameSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { dirname, join } from "node:path";
import { config } from "./project-config.mjs";

export function interruptedRoot(repoRoot) {
  return join(repoRoot, ".agent-runs", "interrupted");
}

export function parkDirForSlug(repoRoot, slug) {
  return join(interruptedRoot(repoRoot), slug);
}

/**
 * True when a park directory holds a non-empty patch and/or moved untracked files.
 * A directory that only contains a `.restored` marker is spent and does not count.
 */
export function hasSalvage(parkDir) {
  if (!parkDir || !existsSync(parkDir)) return false;
  const names = readdirSync(parkDir).filter((n) => n !== ".restored" && n !== "README.txt");
  if (names.length === 0) return false;
  if (names.includes("tracked.patch")) {
    const patch = readFileSync(join(parkDir, "tracked.patch"), "utf8");
    if (patch.trim()) return true;
  }
  return names.some((n) => n !== "tracked.patch");
}

/**
 * Apply a parked salvage back onto `repoRoot`.
 *
 * Returns null when there was nothing to restore. On success the park dir is renamed to
 * `<slug>-applied-<timestamp>` so a later retry does not double-apply the same patch.
 *
 * @returns {{ dir: string, applied: string, patched: boolean, untracked: number, error?: string } | null}
 */
export function restoreSalvage(repoRoot, slug) {
  const parkDir = parkDirForSlug(repoRoot, slug);
  if (!hasSalvage(parkDir)) return null;

  let patched = false;
  let error;

  const patchPath = join(parkDir, "tracked.patch");
  if (existsSync(patchPath) && readFileSync(patchPath, "utf8").trim()) {
    let res = spawnSync("git", ["apply", "--whitespace=nowarn", patchPath], {
      cwd: repoRoot,
      encoding: "utf8",
    });
    if (res.status !== 0) {
      res = spawnSync("git", ["apply", "--3way", "--whitespace=nowarn", patchPath], {
        cwd: repoRoot,
        encoding: "utf8",
      });
    }
    if (res.status !== 0) {
      error = (res.stderr || res.stdout || "git apply failed").trim().slice(0, 500);
    } else {
      patched = true;
    }
  }

  let untracked = 0;
  for (const name of readdirSync(parkDir)) {
    if (name === "tracked.patch" || name === ".restored" || name === "README.txt") continue;
    const src = join(parkDir, name);
    if (!statSync(src).isFile() && !statSync(src).isDirectory()) continue;
    const destRel = name.replace(/__/g, "/");
    const dest = join(repoRoot, destRel);
    try {
      mkdirSync(dirname(dest), { recursive: true });
      if (existsSync(dest)) {
        // Keep the parked copy; do not clobber something already in the tree.
        continue;
      }
      renameSync(src, dest);
      untracked += 1;
    } catch {
      // Leave it in the park dir for a human; continue restoring the rest.
    }
  }

  const applied = `${parkDir}-applied-${new Date().toISOString().replace(/[:.]/g, "-")}`;
  try {
    writeFileSync(
      join(parkDir, ".restored"),
      `Restored at ${new Date().toISOString()}\npatched=${patched}\nuntracked=${untracked}\n`,
    );
    renameSync(parkDir, applied);
  } catch {
    // Rename is bookkeeping; a failed rename must not undo a successful apply.
  }

  return { dir: parkDir, applied, patched, untracked, error };
}

/**
 * Brief fragment injected when prior work was restored or the tree is already dirty.
 */
export function salvageBrief({ verifies, verifyOutput }) {
  if (verifies) {
    return `
## SALVAGE — prior attempt work already verifies

Uncommitted work from a previous attempt is in the tree and \`${config.commands.verify}\` already
passes on it. **Do not rebuild the feature. Do not rewrite working files from scratch.**

Your only job:
1. Diff the working tree (\`git status\` / \`git diff\`) and confirm it matches the TASK.
2. Run the department reviews the brief says this change owes on that existing diff — the
   first review tier together in one message, then the next tier once they have reported.
3. Apply only the fixes those reviewers ask for.
4. Re-run verify if you changed anything, then stop. The runner commits.

If a reviewer says the brief was not delivered, fix the gap — still by editing the existing
diff, not by starting over.
`;
  }

  const clip = (verifyOutput || "").trim().slice(-5000);
  return `
## SALVAGE — prior attempt work is in the tree but does not verify yet

Uncommitted work from a previous attempt was restored. **Do not delete it and start over.**
Read \`git status\` / \`git diff\`, then fix the verification failures below with the smallest
change that makes \`${config.commands.verify}\` pass. Keep every part of the prior work that is sound.

Verification output from the salvage check:

${clip || "(no output captured)"}
`;
}

/**
 * Classify porcelain lines the same way the runner does — queue lane moves are not WIP.
 */
export function dirtyOutsideQueueLines(porcelain) {
  return (porcelain || "")
    .split("\n")
    .filter((l) => l.trim() && !l.slice(3).trim().startsWith(".agent-queue/"));
}
