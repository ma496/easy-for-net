#!/usr/bin/env node
/**
 * The static gate: everything that must pass before a change may commit, in one command.
 *
 *   npm run gate                 # every step, stopping at the first failure
 *   npm run gate -- --fast       # skip the production web build, for quick iteration
 *   npm run gate -- --only web   # one area: api | web | engine
 *   npm run gate -- --changed    # only the steps the changed paths can break
 *   npm run gate -- --changed --plan   # say which steps --changed would run, run nothing
 *
 * The API half builds the solution and runs every backend test project. Its integration
 * tests need the PostgreSQL named in appsettings.Testing.json — an unreachable database is
 * a failed gate, never a skipped step, because a gate that passes without running the tests
 * says nothing. The web half is lint, typecheck and unit tests (vitest and eslint both pass
 * on code that fails `tsc`, so all three run), then the production build. The engine half
 * is the framework's own tests and the hook tests.
 *
 * `--changed` is what verify runs. Each step below names the paths that can break it, and
 * only the steps a changed path reaches run: a backend change does not lint the web app, and
 * a documentation change runs nothing. The paths come from the file verify writes
 * (GATE_CHANGED_PATHS_FILE), else from the working tree. A changed path that no step watches
 * and `INERT` does not name runs the whole gate — an unlisted file is never assumed harmless.
 * GATE_FULL=1 (what `npm run verify -- --full` sets) ignores `--changed`.
 */
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { workingTreePaths } from "./lib/changed-paths.mjs";
import { selectGateSteps } from "./lib/gate-scope.mjs";
import { runSync } from "./lib/proc.mjs";
import { REPO_ROOT } from "./lib/project-config.mjs";

const argv = process.argv.slice(2);
const FAST = argv.includes("--fast");
const CHANGED = argv.includes("--changed") && process.env.GATE_FULL !== "1";
const PLAN = argv.includes("--plan");
const onlyAt = argv.indexOf("--only");
const ONLY = onlyAt >= 0 ? argv[onlyAt + 1] : null;

const API_TESTS = join(REPO_ROOT, "src", "backend", "Tests");
const TOOL = join(REPO_ROOT, "tool");
const WEB = join(REPO_ROOT, "src", "frontend", "web");

/** Test projects of any tooling under `tool/` — a `*.Tests` directory each. None in most checkouts. */
const toolTestDirs = existsSync(TOOL)
  ? readdirSync(TOOL, { withFileTypes: true })
      .filter((d) => d.isDirectory() && d.name.endsWith(".Tests"))
      .map((d) => join("tool", d.name))
  : [];

/** The solution at the root when there is one, else the test project (which builds the API). */
function buildTarget() {
  const solution = readdirSync(REPO_ROOT).find((f) => /\.slnx?$/.test(f));
  return solution ?? relative(REPO_ROOT, API_TESTS);
}

// --- what each step can be broken by ---------------------------------------------------
// A path belongs in a step's list when a change to it can make that step fail. Reads across
// areas count: the web's locale tests read the API's resource files, the tool's tests read
// the template's appsettings.json, and the hooks import the engine's config reader.
const DOTNET = [
  /^src\/backend\//,
  /^global\.json$/,
  /^[^/]+\.slnx?$/,
  /^Directory\.[^/]+$/,
  /^\.config\/dotnet-tools\.json$/,
  /^\.editorconfig$/,
];
const TOOL_PATHS = [/^tool\//, /^global\.json$/, /^[^/]+\.slnx?$/, /^src\/backend\/Source\/appsettings[^/]*\.json$/];
const WEB_PATHS = [/^src\/frontend\/web\//];
const WEB_TESTS = [...WEB_PATHS, /^src\/backend\/Source\/Features\/Localization\/Core\/Resources\//];
const ENGINE = [/^scripts\//, /^agentic\.config\.json$/, /^package(-lock)?\.json$/];
const HOOKS = [/^\.claude\/hooks\//, /^\.claude\/settings\.json$/, /^scripts\/lib\//, /^agentic\.config\.json$/];

/** Paths no step needs to see: prose, queue bookkeeping, run records, editor settings. */
const INERT = [
  /\.md$/,
  /^docs\//,
  /^specs\//,
  /^\.agent-queue\//,
  /^\.agent-runs\//,
  /^\.claude\/memory\//,
  /^\.claude\/settings\.local\.json$/,
  /^\.vscode\//,
  /^\.git(ignore|attributes)$/,
  /^LICENSE$/,
  /\.DotSettings(\.user)?$/,
];

/** Each step: the area it belongs to, what it is called, how to run it, and what it watches. */
const steps = [
  {
    area: "api",
    name: "dotnet build",
    cmd: "dotnet",
    args: ["build", buildTarget(), "-nologo", "-v", "q"],
    watches: DOTNET,
    // A running API from this checkout holds its bin/ open, and on Windows the build then
    // fails with MSB3027 for a reason that has nothing to do with the change.
    before: () => runSync("node", ["scripts/stop-api.mjs"], { cwd: REPO_ROOT, stdio: "inherit" }),
  },
  { area: "api", name: "backend tests", cmd: "dotnet", args: ["test", relative(REPO_ROOT, API_TESTS), "--no-build", "-nologo"], watches: DOTNET },
  ...toolTestDirs.map((dir) => ({ area: "api", name: `tests: ${dir}`, cmd: "dotnet", args: ["test", dir, "-nologo"], watches: TOOL_PATHS })),
  {
    area: "web",
    name: "npm ci",
    cmd: "npm",
    args: ["ci", "--no-audit", "--no-fund"],
    cwd: WEB,
    watches: WEB_TESTS,
    // Only on a fresh checkout — an unattended run cannot type it, and without it every
    // later web step fails for a reason that has nothing to do with the change.
    when: () => !existsSync(join(WEB, "node_modules")),
  },
  { area: "web", name: "eslint", cmd: "npm", args: ["run", "lint"], cwd: WEB, watches: WEB_PATHS },
  { area: "web", name: "tsc --noEmit", cmd: "npx", args: ["tsc", "--noEmit"], cwd: WEB, watches: WEB_PATHS },
  { area: "web", name: "vitest", cmd: "npm", args: ["run", "test"], cwd: WEB, watches: WEB_TESTS },
  { area: "engine", name: "engine tests", cmd: "node", args: ["scripts/test.mjs"], watches: ENGINE },
  { area: "engine", name: "hook tests", cmd: "node", args: [".claude/hooks/hook-tests.mjs"], watches: HOOKS },
  { area: "web", name: "next build", cmd: "npm", args: ["run", "build"], cwd: WEB, watches: WEB_PATHS, when: () => !FAST },
];

/** The paths `--changed` judges: the list verify wrote, else the working tree. */
function changedPaths() {
  const file = process.env.GATE_CHANGED_PATHS_FILE;
  if (file && existsSync(file)) {
    return readFileSync(file, "utf8")
      .split("\n")
      .map((p) => p.trim())
      .filter(Boolean);
  }
  return workingTreePaths(REPO_ROOT);
}

const runnable = steps.filter((s) => (!ONLY || s.area === ONLY) && (!s.when || s.when()));
let planned = runnable;
if (CHANGED) {
  const scope = selectGateSteps(runnable, changedPaths(), { inert: INERT });
  console.log(`gate --changed: ${scope.reason}.`);
  for (const p of scope.unknown.slice(0, 8)) console.log(`  unwatched: ${p}`);
  planned = scope.steps;
}

if (PLAN) {
  console.log(planned.length ? planned.map((s) => `  would run: ${s.name}`).join("\n") : "  would run: nothing");
  process.exit(0);
}

const results = [];
for (const step of planned) {
  console.log(`\n=== gate: ${step.name} ===`);
  const started = Date.now();
  step.before?.();
  const res = runSync(step.cmd, step.args, { cwd: step.cwd ?? REPO_ROOT, stdio: "inherit" });
  const seconds = ((Date.now() - started) / 1000).toFixed(1);
  const ok = res.status === 0;
  results.push({ name: step.name, ok, seconds });
  if (!ok) break;
}

console.log("\n=== gate summary ===");
for (const r of results) console.log(`  ${r.ok ? "PASS" : "FAIL"}  ${r.name}  (${r.seconds}s)`);
const skipped = runnable.filter((s) => !planned.includes(s)).map((s) => s.name);
if (skipped.length) console.log(`  skipped, nothing they watch changed: ${skipped.join(", ")}`);
const failed = results.find((r) => !r.ok);
if (failed) {
  console.log(`\nThe gate failed at "${failed.name}". Nothing after it ran.`);
  process.exit(1);
}
console.log(`\nThe gate passed (${results.length} step(s)${FAST ? ", production web build skipped" : ""}).`);
