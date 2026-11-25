#!/usr/bin/env node
/**
 * Stop this checkout's whole running app — API, web app and dev proxy — once a file under the API
 * project changes.
 *
 * .NET hot reload is not trusted here, so a change to anything under `<backend>/Source/` — C#,
 * the base appsettings, the localization resource JSON — ends the running API instead of
 * leaving it serving the old code, and the web app and proxy go with it so nothing is left
 * half-up against a dead API. `<backend>` is each app's `backend` in agentic.config.json;
 * the test project and the web app are not watched, and neither is a file outside this checkout
 * (another clone or worktree runs its own app). The stopping is `scripts/stop-app.mjs --quick`,
 * which touches only this checkout's `pnpm dev`, dev proxy, `next dev`, browser-check stack and
 * API, and returns at once when nothing can be running.
 *
 * Two ways a file changes, two ways of noticing:
 *
 *   - Edit / Write / NotebookEdit (PostToolUse): the call names the file, so its path decides.
 *   - Bash / PowerShell: a command names no file it writes (`sed -i`, a heredoc, `Set-Content`,
 *     a script), so the command text is not trusted. PreToolUse records a fingerprint of the
 *     watched files git sees as changed or untracked — size and modified time of each — keyed by
 *     the call's `tool_use_id`; PostToolUse takes it again and stops the app when they differ.
 *     A command that fails ends in PostToolUseFailure instead, and is compared the same way: the
 *     `sed -i` before a failing `dotnet build` still changed the file. A file is fingerprinted by
 *     what is on disk, so a commit (status changes, files do not) stops nothing, while a checkout
 *     that moves HEAD compares the watched files between the two commits too. Build output is
 *     gitignored and never counts. git is asked with optional locks off, so the hook never takes
 *     `index.lock` from a git command running beside it.
 *
 * A call refused before it ran (by guard-bash.mjs, or by the person) gets no Post event, so its
 * snapshot is never read; each PreToolUse removes snapshots older than an hour.
 *
 * When something was stopped, or failed to stop, the model is told so (`additionalContext`), so it
 * does not take a refused connection for a bug. This hook never refuses anything: every failure,
 * including a config that does not parse, ends it with exit 0.
 *
 * The config is read here directly rather than through scripts/lib/project-config.mjs, which asks
 * git for the branches as it loads: this runs twice around every shell call.
 *
 * For hook-tests.mjs: STOP_API_ON_EDIT_DRY_RUN=1 prints `would stop` instead of stopping, and
 * STOP_API_ON_EDIT_ROOT points the whole hook at another repository.
 */
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, isAbsolute, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SHELL_TOOLS = new Set(["Bash", "PowerShell"]);
const AFTER_EVENTS = new Set(["PostToolUse", "PostToolUseFailure"]);
const SNAPSHOT_PREFIX = "efn-stop-api-";
const SNAPSHOT_MAX_AGE_MS = 60 * 60 * 1000;
const WINDOWS = process.platform === "win32";
const toSlash = (p) => p.replace(/\\/g, "/");
// Windows paths are compared without regard to case: `d:\repo\...\source\x.cs` is the same file.
const fold = (p) => (WINDOWS ? toSlash(p).toLowerCase() : toSlash(p));

const raw = await new Promise((resolve) => {
  let buf = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (c) => (buf += c));
  process.stdin.on("end", () => resolve(buf));
});

try {
  const event = JSON.parse(raw) ?? {};
  const scriptsRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
  const repoRoot = process.env.STOP_API_ON_EDIT_ROOT ? resolve(process.env.STOP_API_ON_EDIT_ROOT) : scriptsRoot;
  const watched = watchedDirs(repoRoot);

  if (SHELL_TOOLS.has(event.tool_name)) {
    onShellCommand(event, watched, repoRoot, scriptsRoot);
  } else if (AFTER_EVENTS.has(event.hook_event_name ?? "PostToolUse")) {
    onFileEdit(event, watched, repoRoot, scriptsRoot);
  }
} catch {
  // Stopping the app is a convenience; it must never get in the way of the work.
}
process.exit(0);

/** `<backend>/Source` of every app in agentic.config.json, relative to the root, forward slashes. */
function watchedDirs(repoRoot) {
  let apps;
  try {
    const configPath = process.env.AGENTIC_CONFIG ? resolve(process.env.AGENTIC_CONFIG) : join(repoRoot, "agentic.config.json");
    apps = JSON.parse(readFileSync(configPath, "utf8")).apps;
  } catch {
    apps = null; // unreadable or malformed: the guards report that; here the default layout stands
  }
  if (!Array.isArray(apps) || !apps.length) apps = [{ backend: "src/backend" }];
  return apps.filter((app) => typeof app?.backend === "string").map((app) => toSlash(app.backend).replace(/^\/+|\/+$/g, "") + "/Source");
}

/** Edit / Write / NotebookEdit: the path the call names decides. */
function onFileEdit(event, watched, repoRoot, scriptsRoot) {
  const input = event.tool_input ?? {};
  const filePath = input.file_path ?? input.notebook_path ?? "";
  if (!filePath) return;
  const rel = relative(repoRoot, resolve(repoRoot, filePath));
  // Outside the checkout — another clone, a worktree, another drive — is another app's code.
  if (!rel || rel.startsWith("..") || isAbsolute(rel)) return;
  const candidate = fold(rel) + "/";
  if (watched.some((dir) => candidate.startsWith(fold(dir) + "/"))) stopApp(scriptsRoot, event.hook_event_name);
}

/** Bash / PowerShell: fingerprint before, compare after. */
function onShellCommand(event, watched, gitRoot, scriptsRoot) {
  const id = String(event.tool_use_id ?? "");
  if (!id) return;
  const key = createHash("sha1").update(`${toSlash(gitRoot)}|${id}`).digest("hex").slice(0, 16);
  const snapshotFile = join(tmpdir(), `${SNAPSHOT_PREFIX}${key}.json`);

  if (event.hook_event_name === "PreToolUse") {
    sweepStaleSnapshots();
    const snapshot = fingerprint(gitRoot, watched);
    if (snapshot) writeFileSync(snapshotFile, JSON.stringify(snapshot));
    return;
  }
  if (!AFTER_EVENTS.has(event.hook_event_name)) return;

  let before;
  try {
    before = JSON.parse(readFileSync(snapshotFile, "utf8"));
  } catch {
    return; // No snapshot (the Pre hook did not run, or git could not be asked): nothing to compare.
  } finally {
    rmSync(snapshotFile, { force: true });
  }
  const after = fingerprint(gitRoot, watched, Object.keys(before.files));
  if (after && changed(before, after, gitRoot, watched)) stopApp(scriptsRoot, event.hook_event_name);
}

/** Snapshots of calls that never reached a Post event (refused, denied, interrupted). */
function sweepStaleSnapshots() {
  try {
    const dir = tmpdir();
    const now = Date.now();
    for (const name of readdirSync(dir)) {
      if (!name.startsWith(SNAPSHOT_PREFIX)) continue;
      const file = join(dir, name);
      try {
        if (now - statSync(file).mtimeMs > SNAPSHOT_MAX_AGE_MS) rmSync(file, { force: true });
      } catch {
        // Gone already, or another session's sweep got there first.
      }
    }
  } catch {
    // An unreadable temp directory is not this hook's problem.
  }
}

/**
 * Size and modified time of every watched file git reports as modified, added, deleted or
 * untracked, plus `extra` paths (the ones the snapshot before held, so a file restored to its
 * committed state still compares). Null when git cannot be asked.
 */
function fingerprint(gitRoot, watched, extra = []) {
  const head = git(gitRoot, ["rev-parse", "--verify", "-q", "HEAD"]);
  const status = git(gitRoot, ["status", "--porcelain=v1", "-z", "--untracked-files=all", "--", ...watched]);
  if (status === null) return null;
  const paths = new Set(extra);
  const entries = status.split("\0");
  for (let i = 0; i < entries.length; i++) {
    const entry = entries[i];
    if (entry.length < 4) continue;
    paths.add(entry.slice(3));
    if (entry[0] === "R" || entry[0] === "C") i++; // the rename's source path follows; skip it
  }
  const files = {};
  for (const p of paths) {
    try {
      const s = statSync(join(gitRoot, p));
      files[p] = `${s.size}:${s.mtimeMs}`;
    } catch {
      files[p] = "missing";
    }
  }
  return { head: (head ?? "").trim(), files };
}

function changed(before, after, gitRoot, watched) {
  const paths = new Set([...Object.keys(before.files), ...Object.keys(after.files)]);
  // `after` re-stats every path `before` held, so a file committed in between compares equal
  // (same size and time on disk) and a file newly dirtied appears only in `after`.
  for (const p of paths) {
    if ((before.files[p] ?? "clean") !== (after.files[p] ?? "clean")) return true;
  }
  // HEAD moved (checkout, reset, pull, but also a commit). Every file `before` did not list was
  // identical to the old HEAD then; one that now differs from the old HEAD changed on disk. The
  // files `before` did list were compared above, so a commit, which only moves HEAD past them,
  // stops nothing.
  if (before.head && after.head && before.head !== after.head) {
    const diff = git(gitRoot, ["diff", "--name-only", before.head, "--", ...watched]);
    if (diff === null) return true;
    if (diff.split("\n").some((p) => p && !(p in before.files))) return true;
  }
  return false;
}

function git(cwd, args) {
  // GIT_OPTIONAL_LOCKS=0: `status` would otherwise refresh the index and briefly hold index.lock,
  // failing a `git add` or `git commit` that a parallel tool call runs at that moment.
  const res = spawnSync("git", args, { cwd, encoding: "utf8", windowsHide: true, env: { ...process.env, GIT_OPTIONAL_LOCKS: "0" } });
  return res.status === 0 ? res.stdout : null;
}

function stopApp(scriptsRoot, eventName) {
  if (process.env.STOP_API_ON_EDIT_DRY_RUN === "1") {
    console.log("would stop");
    return;
  }
  const res = spawnSync("node", [join(scriptsRoot, "scripts", "stop-app.mjs"), "--quick"], {
    cwd: scriptsRoot,
    encoding: "utf8",
    windowsHide: true,
    timeout: 60_000,
  });
  const out = res.stdout ?? "";
  const survivors = out.match(/still running: ([^.]*)\./)?.[1];
  let message = null;
  if (survivors) {
    message =
      `A file under the API project changed (no hot reload), and stopping this checkout's running app left pid(s) ${survivors} running, ` +
      "which still serve the old code and lock bin/. Run `pnpm stop` (or stop them by hand) before building.";
  } else if (/stopped \d+ process/.test(out)) {
    message =
      "The whole running app (API, web app and dev proxy) was stopped because a file under the API project changed (no hot reload). " +
      "Restart it when it is needed (`pnpm dev`, or `pnpm stack up` for a browser check).";
  }
  if (message) {
    console.log(JSON.stringify({ hookSpecificOutput: { hookEventName: eventName ?? "PostToolUse", additionalContext: message } }));
  }
}
