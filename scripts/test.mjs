#!/usr/bin/env node
/**
 * Unit test runner.
 *
 *   npm test                     # every test
 *   npm test -- product-query    # only files whose path contains "product-query"
 *
 * Uses Node's built-in test runner with tsx as the TypeScript loader — no test framework
 * dependency. Files are discovered and passed explicitly rather than by glob, because Node
 * does not discover `.ts` test files on its own. Which directories hold tests, and what a
 * test file is called, come from `tests` in agentic.config.json.
 */
import { readdirSync, statSync, existsSync } from "node:fs";
import { join, relative } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";
import { config } from "./lib/project-config.mjs";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
// Where this project keeps its unit tests, from agentic.config.json. A root that does
// not exist is skipped rather than failing — a project grows into them.
const TEST_ROOTS = config.tests.roots;
const filter = process.argv[2];
const TEST_PATTERN = new RegExp(config.tests.pattern);

function walk(dir, found = []) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, found);
    else if (TEST_PATTERN.test(entry)) found.push(full);
  }
  return found;
}

const files = TEST_ROOTS.filter((d) => existsSync(join(ROOT, d)))
  .flatMap((d) => walk(join(ROOT, d)))
  .filter((f) => !filter || f.includes(filter))
  .sort();

if (files.length === 0) {
  // A filter that matches nothing is a mistake worth failing on; a repository that has not
  // written its first test yet is not. Failing the second case would mean a fresh install
  // cannot pass its own gate, and the first thing anyone would do about that is delete the
  // test step from the gate.
  if (filter) {
    console.error(`No test files match "${filter}".`);
    process.exit(1);
  }
  console.log(`No test files found under ${TEST_ROOTS.join(", ")} — nothing to run.`);
  process.exit(0);
}

console.log(`Running ${files.length} test file(s):`);
for (const f of files) console.log(`  ${relative(ROOT, f)}`);
console.log("");

// Node's test runner waits for ever by default, and "for ever" is not a test result. One
// test whose bound did not work started a real key derivation, and the unattended loop sat
// on that single file for a quarter of an hour with the whole queue behind it and nothing
// in the log to say why. A hung test is a failing test.
const TEST_TIMEOUT_MS = 60_000;

// tsx is only needed to run TypeScript tests, and only some projects have it installed.
// Loading it unconditionally makes a plain-JavaScript repository fail on a dependency it
// has no reason to carry.
const needsTypeScript = files.some((f) => f.endsWith(".ts"));
const loader = needsTypeScript ? ["--import", "tsx"] : [];

const result = spawnSync(
  process.execPath,
  [...loader, "--test", `--test-timeout=${TEST_TIMEOUT_MS}`, ...files],
  { cwd: ROOT, stdio: "inherit" },
);

process.exit(result.status ?? 1);
