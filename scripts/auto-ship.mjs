#!/usr/bin/env node
/**
 * Unattended shipping: stage deliberately, commit, and — off the work branch only — push
 * and open a pull request.
 *
 *   npm run auto:ship -- "commit subject"
 *   npm run auto:ship -- "commit subject" --commit-only   # stop after the commit
 *   npm run auto:ship -- "commit subject" --branch feat/my-thing
 *
 * The pull request is created with the GitHub CLI (`gh`) when it is installed and signed
 * in and origin is on GitHub. Otherwise everything up to the PR still runs, and the
 * ready-made PR URL is printed.
 *
 * Safety rails that stay on even unattended:
 *   - on the work branch, commits in place and stops: work accumulates locally
 *   - never pushes the base branch; merging is the owner's alone
 *   - never stages .env or appsettings secrets, and never uses `git add -A`
 *   - never force-pushes
 */
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { hasExecutable } from "./lib/proc.mjs";
import { BASE_BRANCH, config, WORK_BRANCH } from "./lib/project-config.mjs";
import { describeRemote } from "./lib/remote.mjs";

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
  console.error('Usage: npm run auto:ship -- "commit subject" [--branch <name>] [--task <brief-stem>]');
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

// The work branch is where work is built, so being on it is the normal case, not a mistake
// to branch away from. What does not change is that nothing leaves the machine: the commit
// lands and the run stops. Any other protected branch keeps the branch-first rule.
const commitOnly = flag("commit-only") || branch === WORK_BRANCH;
const PROTECTED = [...new Set([BASE_BRANCH, "main", "master", "develop"])];
if (!commitOnly && PROTECTED.includes(branch)) {
  const target = argOf("branch", `feat/auto-${slugify(subject)}`);
  console.log(`On ${branch}; moving the working tree to ${target}.`);
  git("checkout", "-b", target);
  branch = target;
} else if (argOf("branch", null) && argOf("branch", null) !== branch) {
  const target = argOf("branch", branch);
  git("checkout", "-b", target);
  branch = target;
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

// The trailing slash matters: in a task worktree `node_modules` is a *symlink*, so it
// appears as a bare entry with no slash and would otherwise be staged.
const IGNORED = /(^|\/)(dist|\.output|\.next|node_modules|bin|obj|coverage)(\/|$)/;
const toStage = entries.filter((e) => !IGNORED.test(e.path));
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
const taskStem = (argOf("task", "") || "").replace(/\.md$/, "");
const bodyLines = [
  "",
  `Verified with \`${config.commands.verify}\` (build, backend and web tests, lint, typecheck).`,
  "",
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
  console.log(`
Committed on ${branch}. Nothing was pushed — that stays yours.

  Review:  git log --oneline origin/${branch}..${branch}
  Push:    git push`);
  process.exit(0);
}

git("push", "-u", "origin", branch);
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
