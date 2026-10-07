#!/usr/bin/env node
/**
 * Unattended shipping: stage deliberately, commit, and — only when asked with `--push`, and
 * never to a protected branch — push and open a pull request.
 *
 *   npm run auto:ship -- "commit subject"                         # commit and stop
 *   npm run auto:ship -- "commit subject" --push                  # then push and open a PR
 *   npm run auto:ship -- "commit subject" --push --branch feat/x  # on a branch of its own
 *   npm run auto:ship -- "commit subject" --verified              # say verify passed (the runner does)
 *
 * The pull request is created with the GitHub CLI (`gh`) when it is installed and signed
 * in and origin is on GitHub. Otherwise everything up to the PR still runs, and the
 * ready-made PR URL is printed.
 *
 * Safety rails that stay on even unattended:
 *   - on the work branch, commits in place and stops: work accumulates locally
 *   - never pushes the base branch; merging is the owner's alone
 *   - never stages .env or appsettings secrets, and never uses `git add -A`
 *   - stages nothing under .agent-queue/ but the task's own brief leaving todo/
 *   - never force-pushes
 */
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { hasExecutable } from "./lib/proc.mjs";
import { BASE_BRANCH, config, PROTECTED_BRANCHES, workflowRefusal } from "./lib/project-config.mjs";
import { describeRemote, unpushedBaseHere } from "./lib/remote.mjs";
import { stageable } from "./lib/stage-paths.mjs";

const argv = process.argv.slice(2);
// `--task <stem>` takes a value; without skipping it the stem would be read as the
// commit subject whenever it appeared before the real one.
const flagsWithValues = new Set(["--branch", "--task"]);
const positional = argv.filter(
  (a, i) => !a.startsWith("--") && !flagsWithValues.has(argv[i - 1]),
);
const subject = positional[0];
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

if (!subject) {
  console.error('Usage: npm run auto:ship -- "commit subject" [--push [--branch <name>]] [--verified] [--task <brief-stem>]');
  process.exit(1);
}

const git = (...args) => {
  const res = spawnSync("git", args, { encoding: "utf8" });
  if (res.status !== 0) {
    throw new Error(`git ${args.join(" ")} failed: ${res.stderr?.trim() || res.stdout?.trim()}`);
  }
  return res.stdout.trim();
};
const gitQuiet = (...args) => spawnSync("git", args, { encoding: "utf8" }).stdout.trim();
// Porcelain status is column-oriented: two status characters, a space, then the path. An
// unstaged modification therefore starts with a space, and trimming the whole output eats
// it off the *first* line only — after which slice(3) takes the first character of the path
// with it, and `git add` fails on a pathspec like "pps/api/...". So this one read keeps its
// leading whitespace and drops only the trailing newline.
const gitLines = (...args) =>
  (spawnSync("git", args, { encoding: "utf8" }).stdout ?? "").replace(/\n+$/, "").split("\n");

function slugify(text) {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 40);
}

// --- 1. branch ----------------------------------------------------------------
let branch = gitQuiet("rev-parse", "--abbrev-ref", "HEAD");

// Committing is the default and the whole contract: nothing leaves the machine unless
// `--push` asks for it. It used to be keyed on `branch === WORK_BRANCH`, and with
// `project.branch` unset the work branch is whatever is checked out — so that was always
// true and the push and pull-request steps below could never run. A protected branch is
// never pushed; with `--push` on one, the work moves to a branch of its own first.
const commitOnly = flag("commit-only") || !flag("push");
const PROTECTED = PROTECTED_BRANCHES;
// `--branch` names a branch to create, so a protected name is refused outright: the push
// below runs inside this process, where the shell guard never sees it, and
// `--push --branch main` was a working route to the branch the guard protects.
// It must also be a plain branch name: `refs/heads/main` is not in the list as a string, yet
// `git push origin refs/heads/main` pushes the real main.
const requested = argOf("branch", null);
if (requested) {
  const plain =
    !/^refs\//.test(requested) &&
    !/[:+~^@]/.test(requested) &&
    spawnSync("git", ["check-ref-format", "--branch", requested], { encoding: "utf8" }).status === 0;
  if (!plain) {
    console.error(`--branch ${requested} is not a plain branch name; name a branch like feat/<slug>.`);
    process.exit(1);
  }
  if (PROTECTED.includes(requested)) {
    console.error(`--branch ${requested} is a protected branch; name a branch of your own.`);
    process.exit(1);
  }
}
if (!commitOnly && PROTECTED.includes(branch)) {
  const target = requested ?? `feat/auto-${slugify(subject)}`;
  console.log(`On ${branch}; moving the working tree to ${target}.`);
  git("checkout", "-b", target);
  branch = target;
} else if (requested && requested !== branch) {
  git("checkout", "-b", requested);
  branch = requested;
}
// Committing is git's own, not the guard's, so the team rule the guard holds for an agent's
// `git commit` is held here too: nothing commits on the base a team shares.
const refusal = workflowRefusal({ branch });
if (refusal) {
  console.error(refusal);
  process.exit(1);
}

// --- 2. stage deliberately ------------------------------------------------------
// Porcelain gives "XY path", two status columns then the path; rename entries carry
// "old -> new". X is the index, Y is the working tree, and the pair decides how a path has
// to be staged — so both are kept rather than sliced away.
const entries = gitLines("status", "--porcelain")
  .filter(Boolean)
  .map((line) => ({
    index: line[0],
    worktree: line[1],
    path: line
      .slice(3)
      .trim()
      .replace(/^.* -> /, "")
      .replace(/^"|"$/g, ""),
  }));

// `.env*` files and the per-environment appsettings hold live credentials and must never be
// staged. The `*.example` templates and the base appsettings.json are the opposite: they
// carry placeholders and exist to be shared, so the check that stops the former must not
// swallow the latter. `guard-bash.mjs` draws the same line and has an allow case for it.
const SECRET_ENV = /(^|\/)\.env(\.|$)/;
const SECRET_APPSETTINGS = /(^|\/)appsettings\.(Development|Testing|Production|Staging)\.json$/i;
const SHARED_ENV_TEMPLATE = /\.example$/;
const forbidden = entries
  .map((e) => e.path)
  .filter((p) => (SECRET_ENV.test(p) && !SHARED_ENV_TEMPLATE.test(p)) || SECRET_APPSETTINGS.test(p));
if (forbidden.length > 0) {
  console.error(`Refusing to stage secret file(s): ${forbidden.join(", ")}`);
  console.error("Remove them from the working tree or add them to .gitignore, then re-run.");
  process.exit(1);
}

// Build output never; queue bookkeeping only as this task's own brief leaving todo/
// (lib/stage-paths.mjs says why). The stem is read here rather than at the commit, because
// it decides what is staged as well as what the trailer says.
const taskStem = (argOf("task", "") || "").replace(/\.md$/, "");
const toStage = stageable(entries, taskStem);
// Leaving a path out of `git add` does not keep it out of the commit when it is already in
// the index, so queue bookkeeping someone staged by hand is unstaged — not reverted — here.
const heldBack = entries
  .filter((e) => e.path.startsWith(".agent-queue/") && !toStage.includes(e) && e.index !== " " && e.index !== "?")
  .map((e) => e.path);
if (heldBack.length > 0) git("reset", "-q", "--", ...heldBack);
if (toStage.length === 0) {
  console.log("Nothing to commit. Working tree is clean.");
  process.exit(0);
}

console.log(`Staging ${toStage.length} path(s):`);
for (const e of toStage) console.log(`  ${e.path}`);

// How a path gets staged depends on where its change is, and getting this wrong fails the
// whole call — `git add` takes one pathspec list, and a single unmatched entry aborts every
// other path with it.
//
//   worktree === " "  already staged in full; naming it again matches nothing, since it is
//                     absent from both the working tree and (for a deletion) the index.
//   worktree === "D"  deleted on disk, so there is no file for a plain `git add` to match;
//                     `-u` stages the removal, still scoped to the paths named here.
//   otherwise         a modification or an untracked file — a normal add.
//
// `-A` would cover all three at once and is exactly what this script promises never to use.
const staged = toStage.filter((e) => e.worktree === " ");
const removed = toStage.filter((e) => e.worktree === "D").map((e) => e.path);
const present = toStage
  .filter((e) => e.worktree !== " " && e.worktree !== "D")
  .map((e) => e.path);

if (present.length > 0) git("add", "--", ...present);
if (removed.length > 0) git("add", "-u", "--", ...removed);
if (staged.length > 0) {
  console.log(`  (${staged.length} already staged)`);
}

// --- 3. commit -------------------------------------------------------------------
// `Task: <brief-stem>` is how the queue's audit ties this commit back to the brief it came
// from. Pairing on subject text alone breaks the moment anyone commits under a different
// message, which left a finished task sitting in todo/ blocking eight dependents.
// Verification is claimed only when the caller ran it: the runner passes --verified after
// its own verify passed. A hand-run auto:ship verified nothing, and the line said otherwise.
const bodyLines = [
  "",
  ...(flag("verified") ? [`Verified with \`${config.commands.verify}\`.`, ""] : []),
  ...(taskStem ? [`Task: ${taskStem}`] : []),
  ...(config.project.coAuthor ? [config.project.coAuthor] : []),
];
git("commit", "-m", subject, "-m", bodyLines.join("\n"));
const sha = gitQuiet("rev-parse", "--short", "HEAD");
console.log(`\nCommitted ${sha} on ${branch}: ${subject}`);

// --- 4. push ----------------------------------------------------------------------
// Committed and stopping. On the work branch this is the whole contract: the owner reviews the
// accumulated commits and pushes them when they are ready, and no script here does it for
// them. Reporting a push that never happened is the one thing worse than not pushing.
if (commitOnly) {
  const since = unpushedBaseHere(branch, BASE_BRANCH);
  console.log(`
Committed on ${branch}. Nothing was pushed — that stays yours.
${since ? `\n  Review:  git log --oneline ${since}..${branch}` : ""}
  Push:    git push`);
  process.exit(0);
}

// The last word before anything leaves the machine, whatever the branch logic above did.
if (PROTECTED.includes(branch)) {
  console.error(`Refusing to push ${branch}: it is a protected branch. The commit stays local.`);
  process.exit(1);
}
// An explicit refspec, so the destination is exactly the branch just checked above and the
// source the branch just committed on — never something git resolves a name to.
git("push", "-u", "origin", `refs/heads/${branch}:refs/heads/${branch}`);
console.log(`Pushed ${branch} to origin.`);

// --- 5. pull request ---------------------------------------------------------------
const described = describeRemote(gitQuiet("remote", "get-url", "origin"));
const printUrl = () => spawnSync("node", [fileURLToPath(new URL("./open-pr.mjs", import.meta.url))], { stdio: "inherit" });

if (described?.forge !== "github" || !hasExecutable("gh")) {
  console.log(
    described?.forge === "github"
      ? "\nThe GitHub CLI (gh) is not installed — PR not created automatically."
      : "\norigin is not on GitHub — PR not created automatically.",
  );
  printUrl();
  process.exit(0);
}

const pr = spawnSync(
  "gh",
  ["pr", "create", "--base", BASE_BRANCH, "--head", branch, "--title", subject, "--body", bodyLines.join("\n").trim()],
  { encoding: "utf8", windowsHide: true },
);
if (pr.status !== 0) {
  console.error(`\nPR creation failed: ${(pr.stderr || pr.stdout || "").trim().slice(0, 300)}`);
  printUrl();
  process.exit(1);
}

console.log(`\nPull request opened: ${pr.stdout.trim()}`);

// Merging is deliberately not implemented. The repo owner merges their own pull
// requests; no script here may do it on their behalf. Do not add a --merge flag back.
console.log(`Review and merge it yourself when you are ready — nothing here will.`);
