import test from "node:test";
import assert from "node:assert/strict";
import { apiAdoptionVerdict, normalizeRoot } from "../lib/api-identity.mjs";
import { parseLesson } from "../lib/memory.mjs";
import { commandParts, hasExecutable, sleepSync, windowsCommandLine } from "../lib/proc.mjs";
import { resolveBaseBranch, resolveWorkBranch } from "../lib/project-config.mjs";
import { describeRemote, forgeOf, parseRemote, pullRequestUrl } from "../lib/remote.mjs";
import {
  launchdLabel,
  posixCommand,
  scheduleEnv,
  schtasksCreateArgs,
  windowsLauncher,
  windowsTaskName,
  windowsWrapper,
} from "../lib/schedule.mjs";

// --- proc.mjs ---------------------------------------------------------------------------

test("commandParts keeps a double-quoted argument together", () => {
  assert.deepEqual(commandParts('node "scripts/a b.mjs" --x'), { cmd: "node", args: ["scripts/a b.mjs", "--x"] });
  assert.deepEqual(commandParts("  npm run gate "), { cmd: "npm", args: ["run", "gate"] });
  assert.deepEqual(commandParts(""), { cmd: undefined, args: [] });
});

test("hasExecutable finds node and refuses a name that is nowhere", () => {
  assert.equal(hasExecutable("node"), true);
  assert.equal(hasExecutable("definitely-not-a-real-binary-4f1c"), false);
});

test("sleepSync blocks for roughly the time asked", () => {
  const started = Date.now();
  sleepSync(60);
  assert.ok(Date.now() - started >= 50);
});

test("the Windows command line quotes only what needs it", () => {
  const line = windowsCommandLine("npm", ["run", "test"]);
  assert.match(line, /^npm run test$/);
});

// --- project-config branch resolution -----------------------------------------------------

test("the work branch is the configured one, else the one checked out", () => {
  assert.equal(resolveWorkBranch("develop", "feat/x"), "develop");
  assert.equal(resolveWorkBranch(null, "feat/x"), "feat/x");
  assert.equal(resolveWorkBranch(null, ""), "main");
});

test("the base branch is configured, else origin/HEAD, else main or master", () => {
  assert.equal(resolveBaseBranch("release", { originHead: "refs/remotes/origin/master" }), "release");
  assert.equal(resolveBaseBranch(null, { originHead: "refs/remotes/origin/master" }), "master");
  assert.equal(resolveBaseBranch(null, { originHead: "", existing: ["master"] }), "master");
  assert.equal(resolveBaseBranch(null, { originHead: "", existing: [] }), "main");
});

// --- api-identity on Windows paths ----------------------------------------------------------

test("a Windows path names one directory however it is spelled", () => {
  assert.equal(normalizeRoot("D:\\Projects\\app\\"), "d:/Projects/app");
  assert.equal(normalizeRoot("d:/Projects/app"), "d:/Projects/app");
});

test("a service reporting the checkout with backslashes is adopted", () => {
  const verdict = apiAdoptionVerdict({ repoRoot: "D:\\Projects\\app" }, "D:\\Projects\\app");
  assert.equal(verdict.adopt, true);
});

test("a service from another checkout is still refused on Windows", () => {
  const verdict = apiAdoptionVerdict({ repoRoot: "D:\\Projects\\other" }, "D:\\Projects\\app");
  assert.equal(verdict.adopt, false);
});

// --- memory.mjs with CRLF --------------------------------------------------------------------

test("a lesson checked out with CRLF line endings still parses", () => {
  const text = "---\r\nscope: gate\r\n---\r\n# Run the gate\r\n\r\nRun it before reporting.\r\n";
  const lesson = parseLesson(text, "run-the-gate.md");
  assert.ok(lesson, "lesson parsed");
  assert.equal(lesson.scope, "gate");
  assert.equal(lesson.title, "Run the gate");
  assert.doesNotMatch(lesson.body, /\r/);
});

// --- remote.mjs ------------------------------------------------------------------------------

test("remotes parse in every common shape", () => {
  assert.deepEqual(parseRemote("git@github.com:o/r.git"), { host: "github.com", slug: "o/r" });
  assert.deepEqual(parseRemote("git@github-work:o/r.git"), { host: "github-work", slug: "o/r" });
  assert.deepEqual(parseRemote("https://github.com/o/r.git"), { host: "github.com", slug: "o/r" });
  assert.deepEqual(parseRemote("ssh://git@gitlab.com:22/o/r.git"), { host: "gitlab.com", slug: "o/r" });
  assert.equal(parseRemote("not a remote"), null);
});

test("an SSH alias resolves through ssh's own config", () => {
  const described = describeRemote("git@github-work:o/r.git", (alias) => (alias === "github-work" ? "github.com" : alias));
  assert.deepEqual(described, { host: "github.com", slug: "o/r", forge: "github" });
  assert.equal(forgeOf("example.com"), null);
});

test("pull request URLs name the base branch", () => {
  assert.equal(
    pullRequestUrl("github", "o/r", "feat/x", "master"),
    "https://github.com/o/r/compare/master...feat%2Fx?expand=1",
  );
  assert.equal(pullRequestUrl(null, "o/r", "x", "main"), null);
});

// --- schedule.mjs ----------------------------------------------------------------------------

const env = scheduleEnv({
  model: "opus",
  maxUsdPerTask: "50",
  maxUsdPerDrain: "off",
  maxRunsPerTask: "3",
  autoPush: false,
  refuseDirty: true,
});

test("the scheduled environment carries every flag explicitly", () => {
  assert.equal(env.AGENT_AUTO_PUSH, "0");
  assert.equal(env.AGENT_REFUSE_DIRTY_START, "1");
  assert.equal(env.AGENT_MAX_USD_PER_DRAIN, "off");
});

test("names are one per project and safe for a scheduler", () => {
  assert.equal(windowsTaskName("Easy For Net"), "easy-for-net-queue-drain");
  assert.equal(launchdLabel("easy-for-net"), "com.easy-for-net.queue-drain");
});

test("the Windows wrapper sets the environment, then calls npm", () => {
  const wrapper = windowsWrapper("D:\\repo", env, "D:\\repo\\.agent-runs\\drain.log");
  assert.match(wrapper, /cd \/d "D:\\repo"/);
  assert.match(wrapper, /set "AGENT_AUTO_PUSH=0"/);
  assert.match(wrapper, /call npm run loop >> "D:\\repo\\\.agent-runs\\drain\.log" 2>&1/);
});

test("the Windows launcher runs the wrapper with no window, and waits for it", () => {
  assert.match(windowsLauncher("D:\\repo\\x.cmd"), /\.Run """D:\\repo\\x\.cmd""", 0, True/);
});

test("schtasks runs every N minutes and replaces an earlier install", () => {
  const args = schtasksCreateArgs("app-queue-drain", 900, "D:\\repo\\x.vbs");
  assert.deepEqual(args.slice(0, 6), ["/Create", "/F", "/SC", "MINUTE", "/MO", "15"]);
  assert.equal(args[args.indexOf("/TR") + 1], 'wscript.exe "D:\\repo\\x.vbs"');
  assert.equal(schtasksCreateArgs("t", 10, "x")[5], "1");
});

test("the POSIX command sets the environment inline", () => {
  assert.match(posixCommand("/repo", env), /^cd "\/repo" && AGENT_MODEL=opus .* npm run loop$/);
});
