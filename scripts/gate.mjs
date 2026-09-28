#!/usr/bin/env node
/**
 * The static gate: everything that must pass before a change may commit, in one command.
 *
 *   npm run gate              # every step, stopping at the first failure
 *   npm run gate -- --fast    # skip the production web build, for quick iteration
 *   npm run gate -- --only web   # one area: api | web | engine
 *
 * The API half builds the solution and runs every backend test project. Its integration
 * tests need the PostgreSQL named in appsettings.Testing.json — an unreachable database is
 * a failed gate, never a skipped step, because a gate that passes without running the tests
 * says nothing. The web half is lint, typecheck and unit tests (vitest and eslint both pass
 * on code that fails `tsc`, so all three run), then the production build. The engine half
 * is the framework's own tests and the hook tests.
 */
import { existsSync, readdirSync } from "node:fs";
import { join, relative } from "node:path";
import { runSync } from "./lib/proc.mjs";
import { REPO_ROOT } from "./lib/project-config.mjs";

const argv = process.argv.slice(2);
const FAST = argv.includes("--fast");
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

/** Each step: the area it belongs to, what it is called, and how to run it. */
const steps = [
  { area: "api", name: "dotnet build", cmd: "dotnet", args: ["build", buildTarget(), "-nologo", "-v", "q"] },
  { area: "api", name: "backend tests", cmd: "dotnet", args: ["test", relative(REPO_ROOT, API_TESTS), "--no-build", "-nologo"] },
  ...toolTestDirs.map((dir) => ({ area: "api", name: `tests: ${dir}`, cmd: "dotnet", args: ["test", dir, "-nologo"] })),
  {
    area: "web",
    name: "npm ci",
    cmd: "npm",
    args: ["ci", "--no-audit", "--no-fund"],
    cwd: WEB,
    // Only on a fresh checkout — an unattended run cannot type it, and without it every
    // later web step fails for a reason that has nothing to do with the change.
    when: () => !existsSync(join(WEB, "node_modules")),
  },
  { area: "web", name: "eslint", cmd: "npm", args: ["run", "lint"], cwd: WEB },
  { area: "web", name: "tsc --noEmit", cmd: "npx", args: ["tsc", "--noEmit"], cwd: WEB },
  { area: "web", name: "vitest", cmd: "npm", args: ["run", "test"], cwd: WEB },
  { area: "engine", name: "engine tests", cmd: "node", args: ["scripts/test.mjs"] },
  { area: "engine", name: "hook tests", cmd: "node", args: [".claude/hooks/hook-tests.mjs"] },
  { area: "web", name: "next build", cmd: "npm", args: ["run", "build"], cwd: WEB, when: () => !FAST },
];

const results = [];
for (const step of steps) {
  if (ONLY && step.area !== ONLY) continue;
  if (step.when && !step.when()) continue;

  console.log(`\n=== gate: ${step.name} ===`);
  const started = Date.now();
  const res = runSync(step.cmd, step.args, { cwd: step.cwd ?? REPO_ROOT, stdio: "inherit" });
  const seconds = ((Date.now() - started) / 1000).toFixed(1);
  const ok = res.status === 0;
  results.push({ name: step.name, ok, seconds });
  if (!ok) break;
}

console.log("\n=== gate summary ===");
for (const r of results) console.log(`  ${r.ok ? "PASS" : "FAIL"}  ${r.name}  (${r.seconds}s)`);
const failed = results.find((r) => !r.ok);
if (failed) {
  console.log(`\nThe gate failed at "${failed.name}". Nothing after it ran.`);
  process.exit(1);
}
console.log(`\nThe gate passed (${results.length} step(s)${FAST ? ", production web build skipped" : ""}).`);
