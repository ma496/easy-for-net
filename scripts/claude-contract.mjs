#!/usr/bin/env node
/**
 * Check that the installed Claude Code CLI still emits what the runner reads.
 *
 *   npm run test:claude-contract                 # run the probes, check the contract
 *   npm run test:claude-contract -- --record     # …and save them as fixtures for this version
 *   npm run test:claude-contract -- --if-changed # skip when this CLI version already passed
 *
 * Everything the runner decides about a session — cost, turns, delegations, skills, whether
 * it hit a limit — is read out of `claude -p --output-format stream-json`, and that format
 * belongs to the CLI. A release that renames a field does not fail anything loudly: the cost
 * reads as unknown, the delegation list reads as empty, and the department check starts
 * refusing good work — or worse, the turn cap stops counting. The unit tests replay streams
 * captured from real versions (`tests/fixtures/claude-stream/`); this is what captures them,
 * and what notices when the live CLI has moved away from the last capture.
 *
 * It makes real model calls, so it is not part of the gate: two short sessions on the
 * cheapest model, a few cents in all. `loop.mjs` runs it with `--if-changed` before a drain,
 * so a CLI update is checked once, before any task spends money on a stream the runner can
 * no longer read. The last result is kept in `.agent-runs/cli-contract.json`.
 *
 * Exit codes: 0 the contract holds, 1 it does not, 4 the CLI could not run (not installed,
 * not signed in, refused) — the same "cannot run" code the runner uses.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { Readable } from "node:stream";
import { fileURLToPath } from "node:url";
import { parseLine, schemaProblems, scrubEventLine } from "./lib/claude-events.mjs";
import { hasExecutable, runSync } from "./lib/proc.mjs";
import { renderStream } from "./lib/stream-render.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const FIXTURES = join(ROOT, "scripts", "tests", "fixtures", "claude-stream");
const STATE_FILE = join(ROOT, ".agent-runs", "cli-contract.json");
const EXIT_BLOCKED = 4;

const argv = process.argv.slice(2);
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};
// The cheapest model is enough: the contract is about the shape of the feed, not the answer.
const MODEL = argOf("model", "haiku");

/** A skill every project has, so the probe can check that loading one is observed. */
function probeSkill() {
  const dir = join(ROOT, ".claude", "skills");
  if (!existsSync(dir)) return null;
  const names = readdirSync(dir, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name);
  return names.includes("coding-conventions") ? "coding-conventions" : (names.sort()[0] ?? null);
}

/**
 * The probes. Each names what it must observe; together they cover every field the runner
 * reads: the result's cost and turns, a delegation and its subagent's own events, a skill
 * load, and a session stopped by `--max-turns` or `--max-budget-usd` that still reports its
 * cost — the two limits the runner hands the CLI.
 */
function probes() {
  const skill = probeSkill();
  return [
    {
      name: "delegation-and-skill",
      args: ["--allowedTools", "Agent,Skill,Glob,Read", "--max-turns", "8"],
      prompt:
        "Do exactly these steps and nothing else: " +
        (skill ? `1) Load the skill named ${skill} with the Skill tool. ` : "1) Skip this step. ") +
        "2) Use the Agent tool with subagent_type general-purpose and the prompt " +
        "'Use the Glob tool once to find package.json at the repository root, then reply with the single word DONE.' " +
        "3) Reply with the single word FINISHED.",
      expect: (res) => [
        typeof res.costUsd === "number" || "no cost was read from the result event",
        res.isError === false || "the result was an error",
        res.turns > 0 || "no parent turns were counted",
        res.subagents.includes("general-purpose") || `the delegation was not observed (saw: ${res.subagents.join(", ") || "none"})`,
        res.subagentTurns > 0 || "the subagent's own turns were not told apart from the parent's",
        !skill || res.skills.includes(skill) || `the skill load was not observed (saw: ${res.skills.join(", ") || "none"})`,
      ],
    },
    {
      name: "max-turns",
      args: ["--allowedTools", "Glob,Read", "--max-turns", "1"],
      prompt: "Use the Glob tool to find package.json, then use the Read tool to read it, then use Glob again for *.md, then summarise.",
      expect: (res) => [
        res.stoppedByCli || `a --max-turns stop was not recognised (subtype: ${res.resultSubtype})`,
        typeof res.costUsd === "number" || "a session stopped by --max-turns reported no cost",
      ],
    },
    {
      name: "max-budget",
      args: ["--allowedTools", "Glob,Read", "--max-budget-usd", "0.001"],
      prompt: "Use the Glob tool to find package.json, then use the Read tool to read it, then use Glob again for *.md, then summarise.",
      expect: (res) => [
        res.stoppedByCli || `a --max-budget-usd stop was not recognised (subtype: ${res.resultSubtype})`,
        typeof res.costUsd === "number" || "a session stopped by --max-budget-usd reported no cost",
      ],
    },
  ];
}

/** Scrub one captured line for committing, with this machine's paths replaced. */
const scrubLine = (line) => scrubEventLine(line, [[ROOT, "<repo>"], [homedir(), "<home>"]]);

function cliVersion() {
  const res = runSync("claude", ["--version"], { encoding: "utf8" });
  return (res.stdout ?? "").trim().split(/\s+/)[0] || null;
}

function readState() {
  try {
    return JSON.parse(readFileSync(STATE_FILE, "utf8"));
  } catch {
    return null;
  }
}

async function runProbe(probe) {
  // The prompt goes in on stdin: a shell on Windows would otherwise have to quote it.
  const res = runSync(
    "claude",
    ["-p", "--model", MODEL, "--output-format", "stream-json", "--verbose", ...probe.args],
    { cwd: ROOT, input: probe.prompt, encoding: "utf8", maxBuffer: 64 * 1024 * 1024, timeout: 5 * 60_000 },
  );
  const stdout = res.stdout ?? "";
  const lines = stdout.split("\n").filter((l) => l.trim());
  const outcome = await renderStream(Readable.from([stdout]), () => {});
  const schema = [];
  for (const line of lines) {
    const raw = parseLine(line);
    if (!raw) continue;
    for (const p of schemaProblems(raw)) schema.push(`${raw.type}${raw.subtype ? `/${raw.subtype}` : ""}: ${p}`);
  }
  const failures = probe.expect(outcome).filter((r) => r !== true);
  return { lines, outcome, schema: [...new Set(schema)], failures, stderr: res.stderr ?? "" };
}

// --- main ---------------------------------------------------------------------------------
if (flag("help")) {
  console.log("Usage: npm run test:claude-contract -- [--record] [--if-changed] [--model <alias>]");
  process.exit(0);
}
if (!hasExecutable("claude")) {
  console.error("The `claude` CLI is not on PATH.");
  process.exit(EXIT_BLOCKED);
}
const version = cliVersion();
const previous = readState();
if (flag("if-changed") && previous?.ok && previous.version === version) {
  console.log(`Claude Code ${version}: the stream contract was already checked on ${previous.checkedAt}.`);
  process.exit(0);
}

console.log(`Checking the stream contract of Claude Code ${version ?? "(unknown version)"} on ${MODEL}…`);
let ok = true;
let ranAny = false;
const results = [];
for (const probe of probes()) {
  const r = await runProbe(probe);
  results.push({ probe, ...r });
  if (r.lines.length === 0) {
    console.error(`  ✗ ${probe.name}: the CLI produced no output.\n${r.stderr.trim().slice(0, 600)}`);
    ok = false;
    continue;
  }
  ranAny = true;
  if (r.outcome.blocked) {
    console.error(`  ✗ ${probe.name}: the account refused the run — ${r.outcome.blocked}`);
    process.exit(EXIT_BLOCKED);
  }
  const problems = [...r.schema, ...r.failures];
  if (problems.length === 0) {
    console.log(`  ✓ ${probe.name}  (${r.lines.length} events, $${(r.outcome.costUsd ?? 0).toFixed(4)})`);
  } else {
    ok = false;
    console.error(`  ✗ ${probe.name}`);
    for (const p of problems) console.error(`      ${p}`);
  }
}

if (!ranAny) process.exit(EXIT_BLOCKED);

if (flag("record") && version) {
  const dir = join(FIXTURES, version);
  mkdirSync(dir, { recursive: true });
  for (const r of results) {
    const scrubbed = r.lines.map(scrubLine).filter(Boolean).join("\n") + "\n";
    writeFileSync(join(dir, `${r.probe.name}.jsonl`), scrubbed);
  }
  console.log(`Recorded ${results.length} fixture(s) in ${dir.replace(ROOT, ".")}. Add their expectations to tests/claude-stream-contract.test.mjs.`);
}

mkdirSync(dirname(STATE_FILE), { recursive: true });
writeFileSync(STATE_FILE, JSON.stringify({ version, ok, model: MODEL, checkedAt: new Date().toISOString() }, null, 2) + "\n");

if (!ok) {
  console.error(
    `\nClaude Code ${version} no longer emits what the runner reads. Fix scripts/lib/claude-events.mjs,\n` +
      "then record fixtures for this version (--record) so the unit tests hold it there.",
  );
  process.exit(1);
}
console.log("The stream contract holds.");
